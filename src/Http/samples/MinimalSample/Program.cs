using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);

builder.EnableOpenApi();

var app = builder.Build();

var todos = app.MapGroup("/todos");
var tasks = new List<Todo>
{
    new Todo(1, "Walk dog", false),
    new Todo(2, "Clean fridge", true)
};

todos.MapGet("", () => tasks);

todos.MapPost("", (Todo todo) =>
{
    tasks.Add(todo);
    return TypedResults.Created<Todo>(todo);
});

todos.MapGet("/{id}", (int id) => {
    tasks.Single(task => task.Id == id);
    });

app.Run();

record Todo(int Id, string Title, bool IsCompleted)
