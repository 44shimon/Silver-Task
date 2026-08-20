using Microsoft.AspNetCore.Mvc;
using Silver_Task.Server.Common;
using Silver_Task.Server.Models.DTOs.CustomFields;
using Silver_Task.Server.Services;

namespace Silver_Task.Server.Controllers
{
    [ApiController]
    [Route("api/custom-fields")]
    public class CustomFieldsController(ICustomFieldService customFieldService) : ControllerBase
    {
        private readonly ICustomFieldService _customFieldService = customFieldService;

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<CustomFieldDto>> GetById(Guid id)
        {
            var field = await _customFieldService.GetByIdAsync(id, User.GetUserId(), User.GetRole());
            return Ok(field.ToDto());
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<CustomFieldDto>> Update(Guid id, [FromBody] UpdateCustomFieldRequest request)
        {
            var field = await _customFieldService.UpdateAsync(id, request, User.GetUserId(), User.GetRole());
            return Ok(field.ToDto());
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _customFieldService.DeleteAsync(id, User.GetUserId(), User.GetRole());
            return NoContent();
        }

        [HttpPost("{id:guid}/options")]
        public async Task<ActionResult<CustomFieldOptionDto>> AddOption(Guid id, [FromBody] CustomFieldOptionRequest request)
        {
            var option = await _customFieldService.AddOptionAsync(id, request.Value, User.GetUserId(), User.GetRole());
            return CreatedAtAction(nameof(GetById), new { id }, option.ToDto());
        }

        [HttpPut("{id:guid}/options/{optionId:guid}")]
        public async Task<ActionResult<CustomFieldOptionDto>> UpdateOption(Guid id, Guid optionId, [FromBody] CustomFieldOptionRequest request)
        {
            var option = await _customFieldService.UpdateOptionAsync(id, optionId, request.Value, User.GetUserId(), User.GetRole());
            return Ok(option.ToDto());
        }

        [HttpDelete("{id:guid}/options/{optionId:guid}")]
        public async Task<IActionResult> DeleteOption(Guid id, Guid optionId)
        {
            await _customFieldService.DeleteOptionAsync(id, optionId, User.GetUserId(), User.GetRole());
            return NoContent();
        }
    }
}
