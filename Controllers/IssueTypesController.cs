using Alt_Support.Models;
using Alt_Support.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Alt_Support.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class IssueTypesController : ControllerBase
    {
        private readonly IJiraService _jiraService;

        public IssueTypesController(
            IJiraService jiraService)
        {
            _jiraService = jiraService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<IssueTypeInfo>>> GetIssueTypes()
        {
            var issueTypes = await _jiraService.GetIssueTypesAsync();
            return Ok(issueTypes.OrderBy(it => it.Name));
        }
    }
}
