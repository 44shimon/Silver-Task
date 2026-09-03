using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Models.DTOs.Admin;
using Silver_Task.Server.Models.Entities.Enums;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    /// <summary>Phase 62 — administer organization-wide API credentials: service accounts (User
    /// rows with IsServiceAccount=true) and their API keys. Administrator-only this phase (matches
    /// the spec's own "administer organization-wide API credentials" framing) — a self-service
    /// "create my own personal key" endpoint is a reasonable future addition, not built here. Thin
    /// — delegates every operation to IApiKeyService; see that interface's own doc comment.</summary>
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    public class AdminApiKeysController(IApiKeyService apiKeyService) : ControllerBase
    {
        private readonly IApiKeyService _apiKeyService = apiKeyService;

        [HttpGet("service-accounts")]
        public async Task<ActionResult<IReadOnlyList<ServiceAccountDto>>> GetServiceAccounts()
        {
            var accounts = await _apiKeyService.GetAllServiceAccountsAsync();
            return Ok(accounts.Select(a => a.ToServiceAccountDto()));
        }

        [HttpPost("service-accounts")]
        public async Task<ActionResult<ServiceAccountDto>> CreateServiceAccount([FromBody] CreateServiceAccountRequest request)
        {
            var account = await _apiKeyService.CreateServiceAccountAsync(request.Name, request.Role, User.GetUserId());
            return CreatedAtAction(nameof(GetServiceAccounts), account.ToServiceAccountDto());
        }

        [HttpDelete("service-accounts/{id:guid}")]
        public async Task<IActionResult> DeactivateServiceAccount(Guid id)
        {
            await _apiKeyService.DeactivateServiceAccountAsync(id, User.GetUserId());
            return NoContent();
        }

        [HttpGet("api-keys")]
        public async Task<ActionResult<IReadOnlyList<ApiKeyDto>>> GetApiKeys()
        {
            var keys = await _apiKeyService.GetAllApiKeysAsync();
            return Ok(keys.Select(k => k.ToDto()));
        }

        [HttpGet("api-keys/{id:guid}")]
        public async Task<ActionResult<ApiKeyDto>> GetApiKey(Guid id)
        {
            var key = await _apiKeyService.GetApiKeyByIdAsync(id);
            return Ok(key.ToDto());
        }

        [HttpPost("api-keys")]
        public async Task<ActionResult<ApiKeyCreatedDto>> CreateApiKey([FromBody] CreateApiKeyRequest request)
        {
            var (key, plaintext) = await _apiKeyService.CreateApiKeyAsync(request.UserId, request.Name, request.ExpiresAt, User.GetUserId());
            return CreatedAtAction(nameof(GetApiKey), new { id = key.Id }, key.ToCreatedDto(plaintext));
        }

        [HttpPost("api-keys/{id:guid}/rotate")]
        public async Task<ActionResult<ApiKeyCreatedDto>> RotateApiKey(Guid id)
        {
            var (key, plaintext) = await _apiKeyService.RotateApiKeyAsync(id, User.GetUserId());
            return Ok(key.ToCreatedDto(plaintext));
        }

        [HttpDelete("api-keys/{id:guid}")]
        public async Task<IActionResult> RevokeApiKey(Guid id)
        {
            await _apiKeyService.RevokeApiKeyAsync(id, User.GetUserId());
            return NoContent();
        }
    }
}
