// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
}).AddCookie()
   .AddGoogle(options => {
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
});
builder.Services.AddAuthorization();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolver = new DefaultJsonTypeInfoResolver());

builder.UseOpenApi();

var app = builder.Build();

app.UseOpenApi();

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
});

app.Run();

record TodoList(int Id, List<TodoTask> Tasks, string owner);
record TodoTask(int Id, [property:JsonPropertyName("title")] string Tite, bool IsCompleted);
