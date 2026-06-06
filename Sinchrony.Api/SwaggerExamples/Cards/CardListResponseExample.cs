using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Cards;

public class CardListResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        data = new[]
        {
            new
            {
                id = "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                lastDigits = "4242",
                brand = "VISA",
                holderName = "MATEUS RODRIGUES",
                expiryDate = "12/28",
                isDefault = true,
                nickname = "Meu Visa",
                token = "tok_..."
            }
        }
    };
}