using System.Text.Json.Serialization;
using Dapr;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment()) { app.UseDeveloperExceptionPage(); }

// Dapr will send serialized event object vs. being raw CloudEvent
app.UseCloudEvents();

// needed for Dapr pub/sub routing
app.MapSubscribeHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

const string DAPR_STORE_NAME = "orderstore-redis";
var client = new Dapr.Client.DaprClientBuilder().Build();

app.MapGet("/orders/{id}", async (int id) =>
{
    // Get state from the state store
    var state = await client.GetStateAsync<Order>(DAPR_STORE_NAME, id.ToString());
    if (state != null)
    {
        return Results.Ok(state);
    }
    else
    {
        return Results.NotFound();
    }
})
.WithName("GetOrder")
.WithOpenApi();

app.MapPost("/orders", async (Order order) =>
{
    // Get state from the state store
    // Publish an event/message using Dapr PubSub
    await client.PublishEventAsync("order-pubsub-redis", "orders", order);
    Console.WriteLine("Published data: " + order);

    return Results.NoContent();
})
.WithName("CreateOrder")
.WithOpenApi();

await app.RunAsync();

public record Order([property: JsonPropertyName("orderId")] int OrderId);
