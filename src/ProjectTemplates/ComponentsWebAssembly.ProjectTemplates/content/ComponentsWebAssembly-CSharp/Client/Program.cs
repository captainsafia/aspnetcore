using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

#if (Hosted)
namespace ComponentsWebAssembly_CSharp.Client
#else
namespace ComponentsWebAssembly_CSharp
#endif
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("app");

            builder.Services.AddSingleton(new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
#if (IndividualLocalAuth)
    #if (Hosted)
            builder.Services.AddApiAuthorization();
    #else
            builder.Services.AddOidcAuthentication(options =>
            {
                #if(MissingAuthority)
                // Configure your authentication provider options here.
                // For more information, see https://aka.ms/blazor-standalone-auth
                #endif
                builder.Configuration.Bind("Authority", options.ProviderOptions.Authority);
                builder.Configuration.Bind("ClientId", options.ProviderOptions.ClientId);
            });
    #endif
#endif
#if (IndividualB2CAuth)
            builder.Services.AddMsalAuthentication(options =>
            {

                var authentication = options.ProviderOptions.Authentication;
                builder.Configuration.Bind("Authority", authentication.Authority);
                builder.Configuration.Bind("ClientId", authentication.ClientId);
                builder.Configuration.Bind("ValidateAuthority", authentication.ValidateAuthority)
#if (Hosted)
                options.ProviderOptions.DefaultAccessTokenScopes.Add("https://qualified.domain.name/api.id.uri/api-scope");
#endif
            });
#endif
#if(OrganizationalAuth)
            builder.Services.AddMsalAuthentication(options =>
            {
                var authentication = options.ProviderOptions.Authentication;
                builder.Configuration.Bind("Authority", authentication.Authority);
                builder.Configuration.Bind("ClientId", authentiation.ClientId);
#if (Hosted)
                options.ProviderOptions.DefaultAccessTokenScopes.Add("api://api.id.uri/api-scope");
#endif
            });
#endif

            await builder.Build().RunAsync();
        }
    }
}
