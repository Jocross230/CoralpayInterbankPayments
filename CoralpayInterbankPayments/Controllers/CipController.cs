using CoralPayInterbankPayment.Interface;
using CoralPayInterbankPayment.Model;
using CoralpayInterbankPayments.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static CoralPayInterbankPayment.Model.EncryptionModel;

namespace CoralPayInterbankPayment.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CipController : ControllerBase
    {
        private readonly ICipIncomingService _service;
        private readonly PgpWrapperService _pgpService;
        private readonly ILogger<CipController> _logger;

        public CipController(PgpWrapperService pgpService, ILogger<CipController> logger, ICipIncomingService service)
        {
            _pgpService = pgpService;
            _logger = logger;
            _service = service;
        }
        [HttpPost("PostCredit")]
        [Consumes("text/plain", "application/json")]
        [Produces("text/plain")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> HandleCredit([FromBody] string encryptedPayload)
        {
            if (string.IsNullOrWhiteSpace(encryptedPayload))
                return BadRequest("Encrypted payload is required.");

            var result = await _service.HandleCreditAsync(encryptedPayload);
            return Content(result, "text/plain");
        }

        [HttpPost("NameEnquiry")]
        [Consumes("text/plain", "application/json")]
        public async Task<IActionResult> NameEnquiry([FromBody] string encryptedPayload)
        {
            if (string.IsNullOrWhiteSpace(encryptedPayload))
                return BadRequest("Encrypted payload is required.");

            try
            {
                var encryptedResponse = await _service.ProcessNameEnquiryAsync(encryptedPayload);
                return Content(encryptedResponse, "text/plain");
            }
            catch (ArgumentException argEx)
            {
                return BadRequest(argEx.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Most likely not able to get feedback from the Url");
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpPost("TransactionEnquiry")]
        [Consumes("text/plain", "application/json")]
        public async Task<IActionResult> QueryTransaction([FromBody] string encryptedPayload)
        {
            if (string.IsNullOrWhiteSpace(encryptedPayload))
                return BadRequest("Encrypted payload is required.");

            try
            {
                var encryptedResponse = await _service.HandleTransactionQueryAsync(encryptedPayload);
                return Ok(encryptedResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while handling transaction query (Most likely not able to get feedback from the database).");
                return StatusCode(500, "An unexpected error occurred.");
            }
        }


        
        [HttpPost("encrypt")]
        public async Task<IActionResult> Encrypt([FromBody] EncryptRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PlainText))
                return BadRequest(new { error = "PlainText is required." });

            try
            {
                var encrypted = await _pgpService.EncryptAsync(request.PlainText);
                return Ok(new { EncryptedText = encrypted });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encryption failed");
                return StatusCode(500, new { error = "Encryption failed", details = ex.Message });
            }
        }

        [HttpPost("decrypt")]
        public async Task<IActionResult> Decrypt([FromBody] DecryptRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.EncryptedText))
                return BadRequest(new { error = "EncryptedText is required." });

            try
            {
                var decrypted = await _pgpService.DecryptAsync(request.EncryptedText);
                return Ok(new { PlainText = decrypted });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Decryption failed");
                return StatusCode(500, new { error = "Decryption failed", details = ex.Message });
            }
        }
    }

}

