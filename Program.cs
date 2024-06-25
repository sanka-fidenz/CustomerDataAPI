using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(config =>
{
    config.DocumentName = "CustomerData";
    config.Title = "CustomerData v1";
    config.Version = "v1";
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi(config =>
    {
        config.DocumentTitle = "CustomerData";
        config.Path = "/swagger";
        config.DocumentPath = "/swagger/{documentName}/swagger.json";
        config.DocExpansion = "list";
    });
}

app.MapGet("/", () => "API works!");

app.MapGet("/users", async (string? key, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(key))
    {
        return await db.Users.ToListAsync();
    }
    return await db.Users.Where(u => u.Name.ToUpper().Contains(key.ToUpper())).ToListAsync();
});

// app.MapGet("/users/{id}", async (string id, AppDbContext db) =>
//     await db.Users.FindAsync(id)
//         is User user
//             ? Results.Ok(user)
//             : Results.NotFound());

// app.MapPost("/users", async (User user, AppDbContext db) =>
// {
//     db.Users.Add(user);
//     await db.SaveChangesAsync();

//     return Results.Created($"/users/{user.Id}", user);
// });

app.MapPut("/users/{id}", async (string id, User inputUser, AppDbContext db) =>
{
    var todo = await db.Users.FindAsync(id);

    if (todo is null) return Results.NotFound();

    todo.Name = inputUser.Name;
    todo.Email = inputUser.Email;
    todo.Phone = inputUser.Phone;

    await db.SaveChangesAsync();
    var updatedUser = await db.Users.FindAsync(id);

    return Results.Ok(updatedUser);
})
.AddEndpointFilter(EndpointFilterUtility.updateUser);

app.MapGet("/users/{id}/getDistance", async (string id, double latitude, double longitude, AppDbContext db) =>
{
    var user = await db.Users.FindAsync(id);
    if (user is null)
    {
        return Results.NotFound();
    }

    var distance = CalculationUtility.distance(user.Latitude, user.Longitude, latitude, longitude, 'K');

    return Results.Ok(distance);
})
.AddEndpointFilter(EndpointFilterUtility.getDistance);

app.Run();
