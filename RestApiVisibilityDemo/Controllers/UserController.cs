using Microsoft.AspNetCore.Mvc;

namespace RestApiVisibilityDemo.Controllers;

[ApiController]
[Route("[controller]")]
public class UserController : ControllerBase
{
    [HttpGet(Name = "GetUser")]
    public IEnumerable<User> Get()
    {
        return Enumerable.Range(1, 5).Select(index => new User
        {
            Created = DateTime.Now,
            Name = $"User {index + 1}"
        }).ToArray();
    }

    [HttpPost(Name = "SetUser")]
    public void Set(User _)
    {
        // no implementation
    }

    [HttpDelete(Name = "DeleteUser")]
    public void Delete(User _)
    {
        // no implementation
    }
}