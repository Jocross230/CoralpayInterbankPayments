using CoralPayInterbankPayment.Data;
using CoralPayInterbankPayment.Model;
using CoralpayInterbankPayments.Helper;
using CoralpayInterbankPayments.Interface;
using CoralpayInterbankPayments.Model;
using Microsoft.EntityFrameworkCore;
using System;

namespace CoralpayInterbankPayments.Service
{
    public class TsqWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TsqWorker> _logger;

        public TsqWorker(IServiceProvider serviceProvider, ILogger<TsqWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<CreditDbContext>();
                    var tsqService = scope.ServiceProvider.GetRequiredService<ITsqService>();

                    List<FTSingleRequest> pendingTxns = new();

                    try
                    {
                        pendingTxns = await db.FTSingleRequests
                            .AsNoTracking()
                            .Where(t => t.responseCode == CoralPayResponseCodes.Pending)
                            .OrderBy(t => t.transactionDate)
                            .Take(50)
                            .ToListAsync(stoppingToken);
                    }
                    catch (Exception dbEx)
                    {
                        var msg = $"[{DateTime.UtcNow}] ⚠️ Failed to fetch pending transactions: {dbEx.Message}";
                        FileLogger.Log(msg);
                        _logger.LogError(dbEx, msg);
                        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                        continue;
                    }

                    if (pendingTxns == null || !pendingTxns.Any())
                    {
                        FileLogger.Log($"[{DateTime.UtcNow}] No pending transactions found.");
                        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                        continue;
                    }

                    var pickMessage = $"[{DateTime.UtcNow}] TSQ Worker picked {pendingTxns.Count} transactions for checking.";
                    FileLogger.Log(pickMessage);
                    _logger.LogInformation(pickMessage);

                    foreach (var txn in pendingTxns)
                    {
                        try
                        {
                            if (txn == null)
                            {
                                FileLogger.Log($"[{DateTime.UtcNow}] Skipping NULL transaction record.");
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(txn.sessionId))
                            {
                                FileLogger.Log($"[{DateTime.UtcNow}] Skipping transaction with NULL sessionId.");
                                continue;
                            }

                            txn.paymentRef ??= "N/A";
                            txn.creditAccount ??= "N/A";
                            txn.sourceAccountId ??= "N/A";
                            txn.destinationInstitutionId ??= "N/A";
                            txn.responseMessage ??= "N/A";
                            txn.channel ??= "N/A";

                            var sendingMessage = $"[{DateTime.UtcNow}] Sending TSQ for SessionId={txn.sessionId}";
                            FileLogger.Log(sendingMessage);
                            _logger.LogInformation(sendingMessage);

                            var tsqResponse = await tsqService.QueryTransactionStatusAsync(txn.sessionId);

                            if (tsqResponse == null)
                            {
                                FileLogger.Log($"[{DateTime.UtcNow}] TSQ returned NULL for SessionId={txn.sessionId}. Skipping update.");
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(tsqResponse.ResponseCode))
                            {
                                FileLogger.Log($"[{DateTime.UtcNow}] TSQ ResponseCode is NULL for SessionId={txn.sessionId}. Skipping update.");
                                continue;
                            }

                            
                            if (tsqResponse.ResponseCode == CoralPayResponseCodes.Success)
                            {
                                txn.responseCode = CoralPayResponseCodes.Ready;  
                                txn.responseMessage = "ready";
                            }
                            else
                            {
                                txn.responseCode = tsqResponse.ResponseCode ?? CoralPayResponseCodes.UnknownError;
                                txn.responseMessage = tsqResponse.ResponseMessage ?? "Unknown";
                            }

                            try
                            {
                                db.FTSingleRequests.Update(txn);
                                await db.SaveChangesAsync(stoppingToken);

                                FileLogger.Log($"[{DateTime.UtcNow}] ✅ Transaction updated successfully for SessionId={txn.sessionId} with ResponseCode={txn.responseCode}");
                            }
                            catch (Exception saveEx)
                            {
                                FileLogger.Log($"[{DateTime.UtcNow}] ❌ Failed to update DB for {txn.sessionId}: {saveEx.Message}");
                                _logger.LogError(saveEx, "DB update failed");
                                continue;
                            }
                        }
                        catch (Exception innerEx)
                        {
                            var errMessage = $"[{DateTime.UtcNow}] ❌ TSQ processing failed for {txn?.sessionId ?? "NULL"}: {innerEx.Message}";
                            FileLogger.Log(errMessage);
                            _logger.LogError(innerEx, errMessage);
                            continue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    var errMessage = $"[{DateTime.UtcNow}] ⚠️ Unhandled error in TSQ worker loop: {ex.Message}";
                    FileLogger.Log(errMessage);
                    _logger.LogError(ex, errMessage);
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        } //okay

        /* protected override async Task ExecuteAsync(CancellationToken stoppingToken)
         {
             while (!stoppingToken.IsCancellationRequested)
             {
                 try
                 {
                     using var scope = _serviceProvider.CreateScope();
                     var db = scope.ServiceProvider.GetRequiredService<CreditDbContext>();
                     var tsqService = scope.ServiceProvider.GetRequiredService<ITsqService>();

                     List<FTSingleRequest> pendingTxns = new();

                     try
                     {
                         pendingTxns = await db.FTSingleRequests
                             .AsNoTracking()
                             .Where(t => t.responseCode == CoralPayResponseCodes.Pending)
                             .OrderBy(t => t.transactionDate)
                             .Take(50)
                             .ToListAsync(stoppingToken);
                     }
                     catch (Exception dbEx)
                     {
                         var msg = $"[{DateTime.UtcNow}] ⚠️ Failed to fetch pending transactions: {dbEx.Message}";
                         FileLogger.Log(msg);
                         _logger.LogError(dbEx, msg);
                         await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                         continue;
                     }

                     if (pendingTxns == null || !pendingTxns.Any())
                     {
                         FileLogger.Log($"[{DateTime.UtcNow}] No pending transactions found.");
                         await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                         continue;
                     }

                     var pickMessage = $"[{DateTime.UtcNow}] TSQ Worker picked {pendingTxns.Count} transactions for checking.";
                     FileLogger.Log(pickMessage);
                     _logger.LogInformation(pickMessage);

                     foreach (var txn in pendingTxns)
                     {
                         try
                         {
                             if (txn == null)
                             {
                                 FileLogger.Log($"[{DateTime.UtcNow}] Skipping NULL transaction record.");
                                 continue;
                             }

                             if (string.IsNullOrWhiteSpace(txn.sessionId))
                             {
                                 FileLogger.Log($"[{DateTime.UtcNow}] Skipping transaction with NULL sessionId.");
                                 continue;
                             }

                             txn.paymentRef ??= "N/A";
                             txn.creditAccount ??= "N/A";
                             txn.sourceAccountId ??= "N/A";
                             txn.destinationInstitutionId ??= "N/A";
                             txn.responseMessage ??= "N/A";
                             txn.channel ??= "N/A";

                             var sendingMessage = $"[{DateTime.UtcNow}] Sending TSQ for SessionId={txn.sessionId}";
                             FileLogger.Log(sendingMessage);
                             _logger.LogInformation(sendingMessage);

                             var tsqResponse = await tsqService.QueryTransactionStatusAsync(txn.sessionId);

                             if (tsqResponse == null)
                             {
                                 FileLogger.Log($"[{DateTime.UtcNow}] TSQ returned NULL for SessionId={txn.sessionId}. Skipping update.");
                                 continue;
                             }

                             if (string.IsNullOrWhiteSpace(tsqResponse.ResponseCode))
                             {
                                 FileLogger.Log($"[{DateTime.UtcNow}] TSQ ResponseCode is NULL for SessionId={txn.sessionId}. Skipping update.");
                                 continue;
                             }

                             txn.responseCode = tsqResponse.ResponseCode ?? CoralPayResponseCodes.UnknownError;
                             txn.responseMessage = tsqResponse.ResponseMessage ?? "Unknown";

                             try
                             {
                                 db.FTSingleRequests.Update(txn);
                                 await db.SaveChangesAsync(stoppingToken);

                                 FileLogger.Log($"[{DateTime.UtcNow}] ✅ Transaction updated successfully for SessionId={txn.sessionId}");
                             }
                             catch (Exception saveEx)
                             {
                                 FileLogger.Log($"[{DateTime.UtcNow}] ❌ Failed to update DB for {txn.sessionId}: {saveEx.Message}");
                                 _logger.LogError(saveEx, "DB update failed");
                                 continue;
                             }
                         }
                         catch (Exception innerEx)
                         {
                             var errMessage = $"[{DateTime.UtcNow}] ❌ TSQ processing failed for {txn?.sessionId ?? "NULL"}: {innerEx.Message}";
                             FileLogger.Log(errMessage);
                             _logger.LogError(innerEx, errMessage);
                             continue;
                         }
                     }
                 }
                 catch (Exception ex)
                 {
                     var errMessage = $"[{DateTime.UtcNow}] ⚠️ Unhandled error in TSQ worker loop: {ex.Message}";
                     FileLogger.Log(errMessage);
                     _logger.LogError(ex, errMessage);
                 }

                 await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
             }
         }*/

    }

}
