// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Buffers;
using System.Globalization;
using System.IO.Pipelines;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Numerics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Xunit.Abstractions;
using Moq;

namespace Microsoft.AspNetCore.Routing.Internal;

public partial class RequestDelegateFactoryTests : LoggedTest
{
    public RequestDelegateFactoryTests(ITestOutputHelper output)
    {
        var converter = new Converter(output);
        Console.SetOut(converter);
    }

    [Fact]
    public async Task RequestDelegateFactory_CanInvokeSingleEndpointFilter_ThatModifiesArguments()
    {
        // Arrange
       string HelloName(string name)
       {
            Console.WriteLine(name);
            return $"Hello, {name}!";
       };
       
        var httpContext = CreateHttpContext();

        var responseBodyStream = new MemoryStream();
        httpContext.Response.Body = responseBodyStream;

        httpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["name"] = "TestName"
        });

        // Act
        var factoryResult = RequestDelegateFactory.Create(HelloName, null, new List<IEndpointFilter>() { new ModifyStringArgumentFilter() });
        var requestDelegate = factoryResult.RequestDelegate;
        await requestDelegate(httpContext);

        // Assert
        var responseBody = Encoding.UTF8.GetString(responseBodyStream.ToArray());
        Assert.Equal("Hello, TestNamePrefix!", responseBody);
    }

    [Fact]
    public async Task RequestDelegateFactory_CanInvokeMultipleEndpointFilters_ThatModifyDifferentArguments()
    {
        // Arrange
        string HelloName(string name, int age)
        {
            return $"Hello, {name}! You are {age} years old.";
        };

        var httpContext = CreateHttpContext();

        var responseBodyStream = new MemoryStream();
        httpContext.Response.Body = responseBodyStream;

        httpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["name"] = "TestName",
            ["age"] = "25"
        });

        // Act
        var factoryResult = RequestDelegateFactory.Create(HelloName, null, new List<IEndpointFilter>() { new ModifyIntArgumentFilter(), new ModifyStringArgumentFilter() });
        var requestDelegate = factoryResult.RequestDelegate;
        await requestDelegate(httpContext);

        // Assert
        var responseBody = Encoding.UTF8.GetString(responseBodyStream.ToArray());
        Assert.Equal("Hello, TestNamePrefix! You are 27 years old.", responseBody);
    }

    [Fact]
    public async Task RequestDelegateFactory_CanInvokeSingleEndpointFilter_ThatModifiesBodyParameter()
    {
        // Arrange
        Todo todo = new Todo() { Name = "Write tests ", IsComplete = true };
        string PrintTodo(Todo todo)
        {
            return $"{todo.Name} is {(todo.IsComplete ? "done" : "not done")}.";
        };

        var httpContext = CreateHttpContext();

        var requestBodyBytes = JsonSerializer.SerializeToUtf8Bytes(todo);
        var stream = new MemoryStream(requestBodyBytes);
        httpContext.Request.Body = stream;

        var responseBodyStream = new MemoryStream();
        httpContext.Response.Body = responseBodyStream;

        // Act
        var factoryResult = RequestDelegateFactory.Create(PrintTodo, null, new List<IEndpointFilter>() { new ModifyTodoArgumentFilter() });
        var requestDelegate = factoryResult.RequestDelegate;
        await requestDelegate(httpContext);

        // Assert
        var responseBody = Encoding.UTF8.GetString(responseBodyStream.ToArray());
        Assert.Equal("Write tests is not done.", responseBody);
    }

    [Fact]
    public async Task RequestDelegateFactory_CanInvokeSingleEndpointFilter_ThatModifiedResult()
    {
        // Arrange
        string HelloName(string name)
        {
            Console.WriteLine(name);
            return $"Hello, {name}!";
        };

        var httpContext = CreateHttpContext();

        var responseBodyStream = new MemoryStream();
        httpContext.Response.Body = responseBodyStream;

        httpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["name"] = "TestName"
        });

        // Act
        var factoryResult = RequestDelegateFactory.Create(HelloName, null, new List<IEndpointFilter>() { new ModifyStringResultFilter() });
        var requestDelegate = factoryResult.RequestDelegate;
        await requestDelegate(httpContext);

        // Assert
        var responseBody = Encoding.UTF8.GetString(responseBodyStream.ToArray());
        Assert.Equal("HELLO, TESTNAME!", responseBody);
    }

    [Fact]
    public async Task RequestDelegateFactory_CanInvokeMultipleEndpointFilters_ThatModifyArgumentsAndResult()
    {
        // Arrange
        string HelloName(string name)
        {
            Console.WriteLine(name);
            return $"Hello, {name}!";
        };

        var httpContext = CreateHttpContext();

        var responseBodyStream = new MemoryStream();
        httpContext.Response.Body = responseBodyStream;

        httpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["name"] = "TestName"
        });

        // Act
        var factoryResult = RequestDelegateFactory.Create(HelloName, null, new List<IEndpointFilter>() { new ModifyStringResultFilter(), new ModifyStringArgumentFilter() });
        var requestDelegate = factoryResult.RequestDelegate;
        await requestDelegate(httpContext);

        // Assert
        var responseBody = Encoding.UTF8.GetString(responseBodyStream.ToArray());
        Assert.Equal("HELLO, TESTNAMEPREFIX!", responseBody);
    }

    public class ModifyStringArgumentFilter : IEndpointFilter
    {
        public async ValueTask<object?> RunAsync(EndpointFilterContext context, Func<EndpointFilterContext, ValueTask<object?>> next)
        {
            Console.WriteLine("bar2");
            context.Parameters[0] = context.Parameters[0] != null ? $"{((string)context.Parameters[0])}Prefix" : "NULL";
            return await next(context);
        }
    }

    public class ModifyIntArgumentFilter : IEndpointFilter
    {
        public async ValueTask<object?> RunAsync(EndpointFilterContext context, Func<EndpointFilterContext, ValueTask<object?>> next)
        {
            context.Parameters[1] = ((int)context.Parameters[1]) + 2;
            return await next(context);
        }
    }

    public class ModifyTodoArgumentFilter : IEndpointFilter
    {
        public async ValueTask<object?> RunAsync(EndpointFilterContext context, Func<EndpointFilterContext, ValueTask<object?>> next)
        {
            Todo originalTodo = (Todo)context.Parameters[0];
            originalTodo.IsComplete = !originalTodo.IsComplete;
            context.Parameters[0] = originalTodo;
            return await next(context);
        }
    }

    public class ModifyStringResultFilter : IEndpointFilter
    {
        public async ValueTask<object?> RunAsync(EndpointFilterContext context, Func<EndpointFilterContext, ValueTask<object?>> next)
        {
            Console.WriteLine("foo");
            var previousResult = await next(context);
            if (previousResult is string stringResult)
            {
                return stringResult.ToUpperInvariant();
            }
            return previousResult;
        }
    }

    private class Converter : TextWriter
    {
        ITestOutputHelper _output;
        public Converter(ITestOutputHelper output)
        {
            _output = output;
        }
        public override Encoding Encoding
        {
            get { return Encoding.Default; }
        }

        public override void WriteLine(int number)
        {
            _output.WriteLine(number.ToString());
        }

        public override void WriteLine(string message)
        {
            _output.WriteLine(message);
        }
        public override void WriteLine(string format, params object[] args)
        {
            _output.WriteLine(format, args);
        }
    }
}

