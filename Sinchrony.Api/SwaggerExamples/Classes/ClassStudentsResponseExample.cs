using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Classes;

public class ClassStudentsResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        data = new[]
        {
            new
            {
                id = "a5c10101-5aa0-47a0-ab3d-6189ecec2a99",
                name = "Carlos Silva",
                email = "carlos@email.com",
                avatar = "https://wqswkpblilxaubdoswbc.supabase.co/storage/v1/object/public/sinchrony-avatars/avatars/a5c10101_1784853105.jpg",
                phone = "63984745681",
                bikeNumber = 3,
                status = "confirmed"
            }
        }
    };
}