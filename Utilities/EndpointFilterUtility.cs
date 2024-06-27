using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Primitives;

class EndpointFilterUtility
{
    public static async ValueTask<object?> getDistance(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var queries = context.HttpContext.Request.Query;
        var errors = new Dictionary<string, string[]>();

        // validate "latitude"
        if (queries.TryGetValue("latitude", out StringValues latitude))
        {
            if (!double.TryParse(latitude, out double d))
            {
                errors.Add("latitude", ["Should be a number"]);
            }
        }
        else
        {
            errors.Add("latitude", ["Cannot be empty"]);
        }

        // validate "longitude"
        if (queries.TryGetValue("longitude", out StringValues longitude))
        {
            if (!double.TryParse(longitude, out double d))
            {
                errors.Add("longitude", ["Should be a number"]);
            }
        }
        else
        {
            errors.Add("longitude", ["Cannot be empty"]);
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        return await next(context);
    }

    public static async ValueTask<object?> updateUser(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var taskArgument = context.GetArgument<User>(1);
        var errors = new Dictionary<string, string[]>();

        // validate email
        var email = new EmailAddressAttribute();
        if (!email.IsValid(taskArgument.Email))
        {
            errors.Add(nameof(User.Email), ["Invalid email address"]);
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }
        return await next(context);
    }
}