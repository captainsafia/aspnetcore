// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

var builder = new OpenApiWebApplicationBuilder(new());

builder.Authentication.AddCookie()
.AddGoogle(options =>
   {
       options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
       options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
   });
builder.Services.AddAuthorization();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolver = new DefaultJsonTypeInfoResolver());

builder.OpenApi.Enable()
    .ConfigureOptions(options =>
    {
        options.JsonRoute = "swagger-doc.json";
    })
    .WithDocument(document =>
    {
        document.Info.Description = "This is a test description";
    });


var app = builder.Build();

var task1 = new TodoTask(1, "Write tests", false);
var task2 = new TodoTask(2, "Walk dog", true);
var task3 = new TodoTask(3, "Mop floors", true);
var task4 = new TodoTask(4, "Rinse sink", false);

var todo1 = new TodoList(1, new List<TodoTask> { task1, task2 }, "bob");
var todo2 = new TodoList(2, new List<TodoTask> { task3, task4 }, "alice");

var lists = new List<TodoList> { todo1, todo2 };

static string Plaintext(string name) => $"Hello, {name}!";

app.MapGet("/plaintext/{name}", Plaintext).WithOpenApi(o =>
{
    o.Summary = "This is a summary";
    return o;
});
app.MapGet("/plaintext-secured/{name}", Plaintext);

app.MapGet("/todo-lists", () => lists);
app.MapGet("/todo-lists/{id}", (int id) => lists.Single(list => list.Id == id));
app.MapGet("/todo-lists/{id}/tasks", (int id) => lists.Single(list => list.Id == id)?.Tasks);
app.MapPost("/todo-lists/{id}", (int id, TodoTask todoTask) =>
{
    lists.Single(list => list.Id == id).Tasks.Add(todoTask);
})
.RequireAuthorization();

app.MapPost("/inheritance", (SubType1 foo) => foo);
app.MapPost("/polymorphism", (BaseType foo) => foo);

app.Run();

class TodoList
{
    public TodoList(int id, List<TodoTask> tasks, string owner)
    {
        Id = id;
        Tasks = tasks;
        Owner = owner;
    }

    /// <summary>
    /// The ID associated with this list.
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// The collection of tasks in this list.
    /// </summary>
    public List<TodoTask> Tasks { get; set; } = new();
    /// <summary>
    /// Represents who is assigned the task.
    /// </summary>
    public string Owner { get; set; } = string.Empty;
}
record TodoTask(int Id, [property:JsonPropertyName("title")] string Tite, bool IsCompleted);

[JsonDerivedType(typeof(SubType1))]
[JsonDerivedType(typeof(SubType2))]
class BaseType
{
    public string BaseProperty { get; set; }
}

class SubType1 : BaseType
{
    public int Property1 { get; set; }
}

class SubType2 : BaseType
{
    public int Property2 { get; set; }
}
