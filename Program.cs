using System.Text.Json.Serialization;
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
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
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
        return await db.Users.Include(u => u.Address).ToListAsync();
    }
    return await db.Users.Where(u => u.Name.ToUpper().Contains(key.ToUpper())).Include(u => u.Address).ToListAsync();
});

app.MapGet("/usersGroupByZipCode", async (AppDbContext db) =>
{
    return await db.Users.Include(u => u.Address).GroupBy(u => u.Address.Zipcode).ToListAsync();
});

app.MapPost("/users", async (User user, AppDbContext db) =>
{

    if (string.IsNullOrEmpty(user.Id))
    {
        user.Id = Guid.NewGuid().ToString();
    }

    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Created($"/users/{user.Id}", user);
});

app.MapPut("/users/{id}", async (string id, User inputUser, AppDbContext db) =>
{
    var todo = await db.Users.FindAsync(id);

    if (todo is null) return Results.NotFound();

    if (!string.IsNullOrWhiteSpace(inputUser.Name))
    {
        todo.Name = inputUser.Name;
    }
    if (!string.IsNullOrWhiteSpace(inputUser.Email))
    {
        todo.Email = inputUser.Email;
    }
    if (!string.IsNullOrWhiteSpace(inputUser.Phone))
    {
        todo.Phone = inputUser.Phone;
    }

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
