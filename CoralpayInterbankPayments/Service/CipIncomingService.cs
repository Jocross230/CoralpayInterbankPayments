using CoralPayInterbankPayment.Data;
using CoralPayInterbankPayment.Interface;
using CoralPayInterbankPayment.Model;
using CoralpayInterbankPayments.Helper;
using CoralpayInterbankPayments.Interface;
using CoralpayInterbankPayments.Model;
using CoralpayInterbankPayments.Service;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

public class CipIncomingService : ICipIncomingService
{
    private readonly PgpWrapperService _pgpService;
    private readonly ITsqService _tsqService;
    private readonly ILogger<CipIncomingService> _logger;
    private readonly AccountCodeSettings _accountCodes;
    private readonly EndpointSettings _endpoints;
    private readonly OneNumBaAuth _auth;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly CreditDbContext _db;

    public CipIncomingService(PgpWrapperService pgpService, ITsqService tsqService, ILogger<CipIncomingService> logger, IOptions<AccountCodeSettings> accountCodes,
                              IOptions<EndpointSettings> endpoints,
                              IOptions<OneNumBaAuth> auth,
                              HttpClient httpClient, IConfiguration configuration, CreditDbContext db)
    {
        _pgpService = pgpService;
        _tsqService = tsqService;
        _logger = logger;
        _accountCodes = accountCodes.Value;
        _endpoints = endpoints.Value;
        _auth = auth.Value;
        _httpClient = httpClient;
        _configuration = configuration;
        _db = db;
    }

    public async Task<string> ProcessNameEnquiryAsync(string encryptedPayload)
    {
        NameEnquiryRequest? request = null;
        try
        {
            FileLogger.Log("==== ProcessNameEnquiryAsync START ====");
            var decryptedJson = await _pgpService.DecryptAsync(encryptedPayload);
            FileLogger.Log($"Decrypted payload: {decryptedJson}");

            if (decryptedJson == "96" || string.IsNullOrWhiteSpace(decryptedJson))
            {
                FileLogger.Log("Decryption returned system error code 96");
                return await EncryptErrorAsync(null,
                    CoralPayResponseCodes.CannotResolveAccount,
                    "Account cannot be resolved");

            }


            request = JsonConvert.DeserializeObject<NameEnquiryRequest>(decryptedJson);

            if (request == null
                || string.IsNullOrWhiteSpace(request.SessionId)
                || string.IsNullOrWhiteSpace(request.DestinationInstitutionId)
                || string.IsNullOrWhiteSpace(request.accountId))
            {
                FileLogger.Log("Invalid request payload detected");

                return await EncryptErrorAsync(request,
                    CoralPayResponseCodes.InvalidAccount,
                    "Invalid account",
                    request?.accountId);
            }

            var nameEnquiryResponse = await HandleNameEnquiryAsync(request);

            if (nameEnquiryResponse == null)
            {
                FileLogger.Log("HandleNameEnquiryAsync returned null → Cannot resolve account");
                return await EncryptErrorAsync(request,
                    CoralPayResponseCodes.CannotResolveAccount,
                    "Account cannot be resolved",
                    request.accountId);
            }

            var responseJson = JsonConvert.SerializeObject(nameEnquiryResponse);
            FileLogger.Log($"Returning success response for SessionId={request.SessionId}, AccountId={request.accountId}");
            var encryptedResponse = await _pgpService.EncryptAsync(responseJson);
            FileLogger.Log("==== ProcessNameEnquiryAsync END ====");
            return encryptedResponse;
        }
        catch (Exception ex)
        {
            FileLogger.Log(ex);
            _logger.LogError(ex, "Error processing Name Enquiry");
            FileLogger.Log($"Error processing Name Enquiry: {ex}");

            return await EncryptErrorAsync(request,
                CoralPayResponseCodes.CannotResolveAccount,
                "Account cannot be resolved");
        }
    }
    private async Task<string> EncryptErrorAsync(NameEnquiryRequest? request, string responseCode, string message, string nuban = "")
    {
        var errorResponse = new NameEnquiryResponses
        {
            sessionId = request?.SessionId ?? "",
            destinationInstitutionId = request?.DestinationInstitutionId ?? "",
            accountId = request?.accountId ?? "",
            accountName = "",
            status = "",
            responseCode = responseCode,
            responseMessage = message,
            bvn = "",
            kycLevel = "",
            accountType = "",
            nameEnquiryRef = GenerateNameEnquiryRef()

        };

        var errorJson = JsonConvert.SerializeObject(errorResponse);
        FileLogger.Log("errorResponse");
        
        return await _pgpService.EncryptAsync(errorJson);

    }
    public async Task<NameEnquiryResponses> HandleNameEnquiryAsync(NameEnquiryRequest req)
    {
        var accountPrefix = req.accountId!.Substring(0, 3);
        var response = new NameEnquiryResponses();

        try
        {
            if (accountPrefix == _accountCodes.TrustPayAccountCode)
            {
                return await HandleTrustPayAsync(req);
            }
            else if (accountPrefix == _accountCodes.STBWalletAccountCode)
            {
                return await HandleSTBWalletAsync(req);
            }
            else if (accountPrefix == _accountCodes.OneNumBaAccountCode)
            {
                return await HandleOneNumBaAsync(req);
            }
            else
            {
                return await HandleInternalBankAsync(req);
            }
        }
        catch (Exception ex)
        {
            FileLogger.Log(ex);
            _logger.LogError(ex, "Error processing Name Enquiry for NUBAN {NUBAN}", req.accountId);
            response.responseCode = CoralPayResponseCodes.CannotResolveAccount;
            response.responseMessage = "Account cannot be resolved";

        }

        return response;
    }

    private async Task<NameEnquiryResponses> HandleTrustPayAsync(NameEnquiryRequest req)
    {
        try
        {
            var payload = new
            {
                DestinationInstitutionCode = req.DestinationInstitutionId,
                vNUBAN = req.accountId,
                SessionID = req.SessionId
            };

            var responseJson = await PostJsonAsync(_configuration["Endpoints:TrustPayNameEnquiry"], payload);
            _logger.LogInformation("TrustPay API response received for NUBAN {nuban}", req.accountId);

            if (string.IsNullOrWhiteSpace(responseJson))
            {
                return new NameEnquiryResponses
                {
                    sessionId = req.SessionId,
                    destinationInstitutionId = req.DestinationInstitutionId,
                    accountId = req.accountId,
                    accountName = "",
                    status = "01",
                    responseCode = CoralPayResponseCodes.CannotResolveAccount,
                    responseMessage = "Account cannot be resolved",
                    bvn = "",
                    kycLevel = "",
                    accountType = "",
                    nameEnquiryRef = GenerateNameEnquiryRef()
                    
                };
            }

            var responseObj = JsonConvert.DeserializeObject<JObject>(responseJson);
            string apiResponseCode = responseObj?.Value<string>("responseCode") ?? CoralPayResponseCodes.InvalidAccount;

            if (apiResponseCode != CoralPayResponseCodes.Success)
            {
                return new NameEnquiryResponses
                {
                    sessionId = req.SessionId,
                    destinationInstitutionId = req.DestinationInstitutionId,
                    accountId = req.accountId,
                    accountName = "",
                    status = "02",
                    responseCode = CoralPayResponseCodes.InvalidAccount,
                    responseMessage = "Invalid account",
                    bvn = "",
                    kycLevel = "",
                    accountType = "",
                    nameEnquiryRef = GenerateNameEnquiryRef()
                    
                };
            }

            return new NameEnquiryResponses
            {
                sessionId = req.SessionId,
                destinationInstitutionId = req.DestinationInstitutionId,
                accountId = req.accountId,
                accountName = responseObj.Value<string>("accountName") ?? responseObj.Value<string>("customerName") ?? "",
                status = "01",
                responseCode = CoralPayResponseCodes.Success,
                responseMessage = "Successful",
                bvn = responseObj.Value<string>("bvn") ?? "",
                kycLevel = responseObj.Value<string>("KYCLevel") ?? "",
                accountType = responseObj.Value<string>("accountType") ?? "",
                nameEnquiryRef = GenerateNameEnquiryRef()
                
            };
        }
        catch (Exception ex)
        {
            FileLogger.Log(ex);
            FileLogger.Log($"Error handling TrustPay name enquiry for NUBAN {req.accountId}: {ex}");
            _logger.LogError(ex, "Error handling TrustPay name enquiry for NUBAN {nuban}", req.accountId);
            return new NameEnquiryResponses
            {
                sessionId = req.SessionId,
                destinationInstitutionId = req.DestinationInstitutionId,
                accountId = req.accountId,
                accountName = "",
                status = "01",
                responseCode = CoralPayResponseCodes.CannotResolveAccount,
                responseMessage = "Account cannot be resolved",
                bvn = "",
                kycLevel = "",
                accountType = "",
                nameEnquiryRef = GenerateNameEnquiryRef()
                
            };
        }

    }

    private async Task<NameEnquiryResponses> HandleSTBWalletAsync(NameEnquiryRequest req)
    {
        try
        {
            var payload = new
            {
                DestinationInstitutionCode = req.DestinationInstitutionId,
                vNUBAN = req.accountId,
                SessionID = req.SessionId
            };

            var responseJson = await PostJsonAsync(_configuration["Endpoints:STBWalletNameEnquiry"], payload);
            FileLogger.Log($"STBWallet API response received for NUBAN {req.accountId}");
            _logger.LogInformation("STBWallet API response received for NUBAN {nuban}", req.accountId);

            if (string.IsNullOrWhiteSpace(responseJson))
            {
                return new NameEnquiryResponses
                {
                    sessionId = req.SessionId,
                    destinationInstitutionId = req.DestinationInstitutionId,
                    accountId = req.accountId,
                    accountName = "",
                    status = "01",
                    responseCode = CoralPayResponseCodes.CannotResolveAccount,
                    responseMessage = "Account cannot be resolved",
                    bvn = "",
                    kycLevel = "",
                    accountType = "",
                    nameEnquiryRef = GenerateNameEnquiryRef()
                   
                };
            }

            var responseObj = JsonConvert.DeserializeObject<JObject>(responseJson);
            string apiResponseCode = responseObj?.Value<string>("responseCode") ?? CoralPayResponseCodes.InvalidAccount;

            if (apiResponseCode != CoralPayResponseCodes.Success)
            {
                return new NameEnquiryResponses
                {
                    sessionId = req.SessionId,
                    destinationInstitutionId = req.DestinationInstitutionId,
                    accountId = req.accountId,
                    accountName = "",
                    status = "02",
                    responseCode = CoralPayResponseCodes.InvalidAccount,
                    responseMessage = "Invalid account",
                    bvn = "",
                    kycLevel = "",
                    accountType = "",
                    nameEnquiryRef = GenerateNameEnquiryRef()
                   
                };
            }

            return new NameEnquiryResponses
            {
                sessionId = req.SessionId,
                destinationInstitutionId = req.DestinationInstitutionId,
                accountId = req.accountId,
                accountName = responseObj.Value<string>("accountName") ?? responseObj.Value<string>("customerName") ?? "",
                status = "01",
                responseCode = CoralPayResponseCodes.Success,
                responseMessage = "Successful",
                bvn = responseObj.Value<string>("bvn") ?? "",
                kycLevel = responseObj.Value<string>("KYCLevel") ?? "",
                accountType = responseObj.Value<string>("accountType") ?? "",
                nameEnquiryRef = GenerateNameEnquiryRef()
            };
        }
        catch (Exception ex)
        {
            FileLogger.Log(ex);
            FileLogger.Log($"Error handling STBWallet name enquiry for NUBAN {req.accountId}: {ex}");
            _logger.LogError(ex, "Error handling STBWallet name enquiry for NUBAN {nuban}", req.accountId);
            return new NameEnquiryResponses
            {
                sessionId = req.SessionId,
                destinationInstitutionId = req.DestinationInstitutionId,
                accountId = req.accountId,
                accountName = "",
                status = "01",
                responseCode = CoralPayResponseCodes.CannotResolveAccount,
                responseMessage = "Account cannot be resolved",
                bvn = "",
                kycLevel = "",
                accountType = "",
                nameEnquiryRef = GenerateNameEnquiryRef()
                
            };
        }

    }

    private async Task<NameEnquiryResponses> HandleOneNumBaAsync(NameEnquiryRequest req)
    {
        try
        {
            var payload = new
            {
                DestinationInstitutionCode = req.DestinationInstitutionId,
                vNUBAN = req.accountId,
                SessionID = req.SessionId
            };

            var responseJson = await PostJsonAsync(_configuration["Endpoints:OneNumBaNameEnquiry"], payload);
            FileLogger.Log($"OneNumBa API response received for NUBAN {req.accountId}");
            _logger.LogInformation("OneNumBa API response received for NUBAN {nuban}", req.accountId);

            if (string.IsNullOrWhiteSpace(responseJson))
            {

                return new NameEnquiryResponses
                {
                    sessionId = req.SessionId,
                    destinationInstitutionId = req.DestinationInstitutionId,
                    accountId = req.accountId,
                    accountName = "",
                    status = "01",
                    responseCode = CoralPayResponseCodes.CannotResolveAccount,
                    responseMessage = "Account cannot be resolved",
                    bvn = "",
                    kycLevel = "",
                    accountType = "",
                    nameEnquiryRef = GenerateNameEnquiryRef()
                    
                };

            }

            var responseObj = JsonConvert.DeserializeObject<JObject>(responseJson);
            string apiResponseCode = responseObj?.Value<string>("responseCode") ?? CoralPayResponseCodes.InvalidAccount;

            if (apiResponseCode != CoralPayResponseCodes.Success)
            {
                return new NameEnquiryResponses
                {
                    sessionId = req.SessionId,
                    destinationInstitutionId = req.DestinationInstitutionId,
                    accountId = req.accountId,
                    accountName = "",
                    status = "02",
                    responseCode = CoralPayResponseCodes.InvalidAccount,
                    responseMessage = "Invalid account",
                    bvn = "",
                    kycLevel = "",
                    accountType = "",
                    nameEnquiryRef = GenerateNameEnquiryRef()
                    
                };
            }

            return new NameEnquiryResponses
            {
                sessionId = req.SessionId,
                destinationInstitutionId = req.DestinationInstitutionId,
                accountId = req.accountId,
                accountName = responseObj.Value<string>("accountName") ?? responseObj.Value<string>("customerName") ?? "",
                status = "01",
                responseCode = CoralPayResponseCodes.Success,
                responseMessage = "Successful",
                bvn = responseObj.Value<string>("bvn") ?? "",
                kycLevel = responseObj.Value<string>("KYCLevel") ?? "",
                accountType = responseObj.Value<string>("accountType") ?? "",
                nameEnquiryRef = GenerateNameEnquiryRef()
                
            };
        }
        catch (Exception ex)
        {
            FileLogger.Log(ex);
            FileLogger.Log($"Error handling OneNumBa name enquiry for NUBAN {req.accountId}: {ex}");
            _logger.LogError(ex, "Error handling OneNumBa name enquiry for NUBAN {nuban}", req.accountId);
            return new NameEnquiryResponses
            {
                sessionId = req.SessionId,
                destinationInstitutionId = req.DestinationInstitutionId,
                accountId = req.accountId,
                accountName = "",
                status = "01",
                responseCode = CoralPayResponseCodes.CannotResolveAccount,
                responseMessage = "Account cannot be resolved",
                bvn = "",
                kycLevel = "",
                accountType = "",
                nameEnquiryRef = GenerateNameEnquiryRef()
            };
        }

    }

    private Task<NameEnquiryResponses> HandleInternalBankAsync(NameEnquiryRequest req)
    {
        try
        {
            FileLogger.Log("==== HandleInternalBankAsync START ====");
            var accountInfo = SunTrustProxy.getAccountBynumber(req.accountId);

            if (accountInfo == null || accountInfo.responseCode != CoralPayResponseCodes.Success ||
                accountInfo.Items == null || !accountInfo.Items.Any())
            {
                return Task.FromResult(new NameEnquiryResponses
                {
                    sessionId = req.SessionId,
                    destinationInstitutionId = req.DestinationInstitutionId,
                    accountId = req.accountId,
                    accountName = "",
                    status = "02",
                    responseCode = CoralPayResponseCodes.InvalidAccount,
                    responseMessage = "Invalid account",
                    bvn = "",
                    kycLevel = "",
                    accountType = "",
                    nameEnquiryRef = GenerateNameEnquiryRef()
                   
                });
            }

            var item = accountInfo.Items[0];

            return Task.FromResult(new NameEnquiryResponses
            {
                sessionId = req.SessionId,
                destinationInstitutionId = req.DestinationInstitutionId,
                accountId = req.accountId,
                accountName = item.AccountName ?? $"{item.FirstName} {item.LastName}" ?? "",
                status = "01",
                responseCode = CoralPayResponseCodes.Success,
                responseMessage = "Successful",
                bvn = item.Bvn ?? "",
                kycLevel = item.Tier ?? "",
                accountType = item.AccountType ?? "",
                nameEnquiryRef = GenerateNameEnquiryRef()
               
            });

        }
        catch (Exception ex)
        {
            FileLogger.Log(ex);
            FileLogger.Log($"Error handling Internal Bank name enquiry for NUBAN {req.accountId}: {ex}");
            _logger.LogError(ex, "Error handling Internal Bank name enquiry for NUBAN {nuban}", req.accountId);
            return Task.FromResult(new NameEnquiryResponses
            {
                sessionId = req.SessionId,
                destinationInstitutionId = req.DestinationInstitutionId,
                accountId = req.accountId,
                accountName = "",
                status = "01",
                responseCode = CoralPayResponseCodes.CannotResolveAccount,
                responseMessage = "Account cannot be resolved",
                bvn = "",
                kycLevel = "",
                accountType = "",
                nameEnquiryRef = GenerateNameEnquiryRef()
            });
        }

    }
    private string GenerateNameEnquiryRef()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var random = new Random().Next(10000, 99999);
        return $"{timestamp}{random}";
    }


    private async Task<string> PostJsonAsync(string url, object payload)
    {
        var json = JsonConvert.SerializeObject(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content);
        return await response.Content.ReadAsStringAsync();
    }
    public async Task<string> HandleCreditAsync(string encryptedPayload)
    {
        CreditRequest creditRequest = null;
        try
        {
            FileLogger.Log("==== HandleCreditAsync START ====");
            var decryptedJson = await _pgpService.DecryptAsync(encryptedPayload);
            FileLogger.Log($"Decrypted payload: {decryptedJson}");
            if (decryptedJson == "96")
            {
                FileLogger.Log("Decryption returned system error code 96");
                return await EncryptCreditErrorAsync(
                    creditRequest,
                    CoralPayResponseCodes.SystemMalfunction,
                    "System malfunction");
            }

            try
            {
                creditRequest = JsonConvert.DeserializeObject<CreditRequest>(decryptedJson);
            }
            catch (Exception jsonEx)
            {
                FileLogger.Log($"JSON deserialization failed: {jsonEx.Message}");
                return await EncryptCreditErrorAsync(
                    creditRequest,
                    CoralPayResponseCodes.InvalidTransaction,
                    "InvalidTransaction");
            }

            if (creditRequest == null)
            {
                FileLogger.Log("❌ CreditRequest is null after deserialization");
                return await EncryptCreditErrorAsync(
                    creditRequest,
                    CoralPayResponseCodes.InvalidTransaction,
                    "Invalid Transaction ");
            }
            creditRequest.channel = string.IsNullOrWhiteSpace(creditRequest.channel)
                ? "GENERAL"
                : creditRequest.channel.Trim().ToUpperInvariant();

            creditRequest.group = string.IsNullOrWhiteSpace(creditRequest.group)
                ? "DEFAULT"
                : creditRequest.group.Trim().ToUpperInvariant();

            creditRequest.sector = string.IsNullOrWhiteSpace(creditRequest.sector)
                ? "GENERAL"
                : creditRequest.sector.Trim().ToUpperInvariant();

            FileLogger.Log($"[CHANNEL CHECK] Incoming channel='{creditRequest.channel}', group='{creditRequest.group}', sector='{creditRequest.sector}'");


            
            var missingFields = new List<string>();
            if (string.IsNullOrWhiteSpace(creditRequest.sessionId)) missingFields.Add(nameof(creditRequest.sessionId));
            if (string.IsNullOrWhiteSpace(creditRequest.paymentRef)) missingFields.Add(nameof(creditRequest.paymentRef));
            if (string.IsNullOrWhiteSpace(creditRequest.creditAccount)) missingFields.Add(nameof(creditRequest.creditAccount));
            if (creditRequest.amount <= 0) missingFields.Add(nameof(creditRequest.amount));

           

            if (missingFields.Any())
            {
                var msg = $"❌ InvalidTransaction: {string.Join(", ", missingFields)}";
                FileLogger.Log(msg);

                string responseCode =
                    missingFields.Contains(nameof(creditRequest.amount)) ? CoralPayResponseCodes.InvalidAmount :
                    CoralPayResponseCodes.InvalidTransaction;

                return await EncryptCreditErrorAsync(
                    creditRequest,
                    responseCode,
                    $"InvalidTransaction: {string.Join(", ", missingFields)}");
            }

            var existingTx = await _db.FTSingleRequests
                .FirstOrDefaultAsync(x => x.paymentRef == creditRequest.paymentRef);

            if (existingTx != null)
            {
                FileLogger.Log($"⚠️ Duplicate transaction detected for PaymentRef={creditRequest.paymentRef}");
                return await EncryptCreditErrorAsync(
                    creditRequest,
                    CoralPayResponseCodes.DuplicateTransaction,
                    "Duplicate Transaction ");
            }

            var nameResponse = await HandleNameEnquiryAsync(new NameEnquiryRequest
                {
                    SessionId = creditRequest.sessionId,
                    DestinationInstitutionId = creditRequest.destinationInstitutionId,
                    accountId = creditRequest.creditAccount
                });

                if (nameResponse == null || nameResponse.responseCode != CoralPayResponseCodes.Success)
                {
                    FileLogger.Log($"❌ Invalid account detected: {creditRequest.creditAccount}, code={(nameResponse == null ? "NULL" : nameResponse.responseCode)}");
                    return await EncryptCreditErrorAsync(
                        creditRequest,
                        CoralPayResponseCodes.InvalidAccount,
                        "Invalid account number");
                }





            try
            {
                var transaction = new FTSingleRequest
                {
                    sessionId = creditRequest.sessionId,
                    paymentRef = creditRequest.paymentRef,
                    destinationInstitutionId = creditRequest.destinationInstitutionId,
                    creditAccount = creditRequest.creditAccount,
                    creditAccountName = creditRequest.creditAccountName,
                    sourceAccountId = creditRequest.sourceAccountId,
                    sourceAccountName = creditRequest.sourceAccountName,
                    narration = creditRequest.narration,
                    channel = creditRequest.channel,
                    Group = creditRequest.group,
                    sector = creditRequest.sector,
                    amount = creditRequest.amount,
                    nameEnquiryRef = creditRequest.nameEnquiryRef,
                    transactionDate = DateTime.UtcNow,
                    responseCode = CoralPayResponseCodes.Pending,
                    responseMessage = "Pending",
                };

                _db.FTSingleRequests.Add(transaction);
                await _db.SaveChangesAsync();

                FileLogger.Log($"Transaction saved: SessionId={transaction.sessionId}, ResponseCode={transaction.responseCode}");
            }
            catch (Exception dbEx)
            {

                FileLogger.Log($"DB save failed: {dbEx.Message}");
                return await EncryptCreditErrorAsync(
                    creditRequest,
                    CoralPayResponseCodes.SystemMalfunction,
                    "SystemMalfunction");
            }




            var responseToCoralPay = new CreditResponseDto
            {
                sessionId = creditRequest.sessionId,
                paymentRef = creditRequest.paymentRef,
                destinationInstitutionId = creditRequest.destinationInstitutionId,
                creditAccount = creditRequest.creditAccount,
                creditAccountName = creditRequest.creditAccountName,
                sourceAccountId = creditRequest.sourceAccountId,
                sourceAccountName = creditRequest.sourceAccountName,
                narration = creditRequest.narration,
                channel = creditRequest.channel,
                group = creditRequest.group,
                sector = creditRequest.sector,
                amount = creditRequest.amount,
                nameEnquiryRef = creditRequest.nameEnquiryRef,
                transactionDate = DateTime.UtcNow,
                responseCode = CoralPayResponseCodes.Success,
                responseMessage = "successful"
            };

            var responseJson = JsonConvert.SerializeObject(responseToCoralPay);
            FileLogger.Log($"Returning response for SessionId={creditRequest.sessionId}, ResponseCode=00");

            return await _pgpService.EncryptAsync(responseJson);
        }
        catch (Exception ex)
        {
            FileLogger.Log(ex);
            FileLogger.Log($"ERROR in HandleCreditAsync: {ex}");
            _logger.LogError(ex, "Error in HandleCreditAsync");
            return await EncryptCreditErrorAsync(
                creditRequest,
                CoralPayResponseCodes.SystemMalfunction,
                "System malfunction");
        }
    }
    


    public async Task<string> HandleTransactionQueryAsync(string encryptedPayload)
    {
        TransactionQueryRequest? queryRequest = null;
        try
        {
            FileLogger.Log("==== HandleTransactionQuery START ====");
            var decryptedJson = await _pgpService.DecryptAsync(encryptedPayload);
            FileLogger.Log($"Decrypted payload: {decryptedJson}");
            if (decryptedJson == "96")
            {
                FileLogger.Log("Decryption returned system error code 96");
                return await EncryptTransactionQueryErrorAsync(null!, CoralPayResponseCodes.TransactionNotFound, "Transaction Not Found");
            }

            queryRequest = JsonConvert.DeserializeObject<TransactionQueryRequest>(decryptedJson);
            if (queryRequest == null || string.IsNullOrWhiteSpace(queryRequest.SessionId))
            {
                FileLogger.Log("Invalid or empty SessionId — returning TransactionNotFound");
                return await EncryptTransactionQueryErrorAsync(queryRequest!, CoralPayResponseCodes.TransactionNotFound, "Transaction Not Found");
            }

            var transaction = await _db.FTSingleRequests
                .FirstOrDefaultAsync(x => x.sessionId == queryRequest.SessionId);
            FileLogger.Log("Queried FTSingleRequests for transaction");

            if (transaction == null)
            {
                FileLogger.Log($"Transaction not found for SessionId={queryRequest.SessionId}");
                return await EncryptTransactionQueryErrorAsync(queryRequest, CoralPayResponseCodes.TransactionNotFound, "Transaction not found");
            }

            var response = new FTSingleRequest
            {
                sessionId = transaction.sessionId,
                paymentRef = transaction.paymentRef,
                destinationInstitutionId = transaction.destinationInstitutionId,
                creditAccount = transaction.creditAccount,
                creditAccountName = transaction.creditAccountName,
                sourceAccountId = transaction.sourceAccountId,
                sourceAccountName = transaction.sourceAccountName,
                narration = transaction.narration,
                channel = transaction.channel,
                Group = transaction.Group,
                sector = transaction.sector,
                amount = transaction.amount,
                transactionDate = transaction.transactionDate,
                responseCode = transaction.responseCode,
                responseMessage = transaction.responseMessage
            };

            var responseJson = JsonConvert.SerializeObject(response);
            FileLogger.Log($"Returning successful response for SessionId={transaction.sessionId}, ResponseCode={transaction.responseCode}");
            return await _pgpService.EncryptAsync(responseJson);
        }
        catch (Exception ex)
        {
            FileLogger.Log(ex);
            FileLogger.Log($"❌ Exception occurred in HandleTransactionQueryAsync: {ex.Message}");
            _logger.LogError(ex, "Error processing transaction query");

            FileLogger.Log("Returning encrypted TransactionNotFound error response due to exception");
            return await EncryptTransactionQueryErrorAsync(queryRequest, CoralPayResponseCodes.TransactionNotFound, "Transaction not found");
        }
    }
    private async Task<string> EncryptCreditErrorAsync(CreditRequest req, string responseCode, string message)
    {
        var errorResponse = new FTSingleRequest
        {
            sessionId = req?.sessionId ?? "",
            paymentRef = req?.paymentRef ?? "",
            destinationInstitutionId = req?.destinationInstitutionId ?? "",
            creditAccount = req?.creditAccount ?? "",
            creditAccountName = req?.creditAccountName ?? "",
            sourceAccountId = req?.sourceAccountId ?? "",
            sourceAccountName = req?.sourceAccountName ?? "",
            narration = req?.narration ?? "",
            channel = req?.channel ?? "",
            Group = req?.group ?? "",
            sector = req?.sector ?? "",
            amount = req?.amount ?? 0,
            transactionDate = DateTime.UtcNow,
            responseCode = responseCode,
            responseMessage = message,
            nameEnquiryRef = req?.nameEnquiryRef ?? GenerateNameEnquiryRef()
        };

        var errorJson = JsonConvert.SerializeObject(errorResponse);
        return await _pgpService.EncryptAsync(errorJson);
    }
    private async Task<string> EncryptTransactionQueryErrorAsync(TransactionQueryRequest req, string responseCode, string message)
    {
        var errorResponse = new FTSingleRequest
        {
            sessionId = req?.SessionId ?? "",
            paymentRef = "",
            destinationInstitutionId = "",
            creditAccount = "",
            creditAccountName = "",
            sourceAccountId = "",
            sourceAccountName = "",
            narration = "",
            channel = "",
            Group = "",
            sector = "",
            amount = 0,
            transactionDate = DateTime.UtcNow,
            responseCode = responseCode,
            responseMessage = message
        };

        var errorJson = JsonConvert.SerializeObject(errorResponse);
        return await _pgpService.EncryptAsync(errorJson);
    }


}
