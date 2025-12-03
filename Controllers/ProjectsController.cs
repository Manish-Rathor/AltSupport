using Alt_Support.Models;
using Alt_Support.Services;
using Microsoft.AspNetCore.Mvc;

namespace Alt_Support.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectsController : ControllerBase
    {
        private readonly IJiraService _jiraService;

        public ProjectsController(
            IJiraService jiraService)
        {
            _jiraService = jiraService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProjectInfo>>> GetProjects()
        {
            var projects = await _jiraService.GetProjectsAsync();
            return Ok(projects.OrderBy(p => p.Name));
        }
    }
}
