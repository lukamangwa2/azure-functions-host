// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.UI.V3.Pages.Internal.Account;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Script.Middleware;
using Microsoft.Azure.WebJobs.Script.WebHost.Configuration;
using Microsoft.Azure.WebJobs.Script.WebHost.Middleware;
using Microsoft.Azure.WebJobs.Script.WebHost.Security.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Script.Tests.Middleware
{
    public class EasyAuthConfigurationMiddlewareTests
    {
        [Fact]
        public async Task Invoke_EasyAuthEnabled_InvokesNext()
        {
            var easyAuthSettings = new HostEasyAuthOptions
            {
                SiteAuthClientId = "id",
                SiteAuthEnabled = true
            };

            var easyAuthOptions = new OptionsWrapper<HostEasyAuthOptions>(easyAuthSettings);

            bool nextInvoked = false;
            RequestDelegate next = (ctxt) =>
            {
                nextInvoked = true;
                ctxt.Response.StatusCode = (int)HttpStatusCode.Accepted;
                return Task.CompletedTask;
            };

            var middleware = new JobHostEasyAuthMiddleware(easyAuthOptions);
            var httpContext = new DefaultHttpContext();
            await middleware.Invoke(httpContext, next);

            Assert.True(nextInvoked);
            // TODO - assertions for easy auth settings passed in? or that easyauth succeeded generally?
        }

        [Fact]
        public async Task Invoke_EasyAuthEnabled()
        {
            // enable easyauth
            // should return 401 unauthorized
            var envVars = new Dictionary<string, string>()
            {
                { EnvironmentSettingNames.ContainerName, "foo" },
            };
            var testEnv = new TestEnvironment(envVars);
            var easyAuthSettings = new HostEasyAuthOptions
            {
                SiteAuthClientId = "id",
                SiteAuthEnabled = true
            };

            // is this how to test easyauth?
            var claims = new List<Claim>
                {
                    new Claim(SecurityConstants.AuthLevelClaimType, AuthorizationLevel.Function.ToString())
                };
            var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

            var builder = new WebHostBuilder()
                .Configure(app =>
                {
                    app.UseMiddleware<JobHostPipelineMiddleware>();
                    app.Run(async context =>
                    {
                        await context.Response.WriteAsync("test easy auth");
                    });
                }).ConfigureServices(services =>
                {
                    services.AddTransient<IEnvironment>(factory => testEnv);
                    services.ConfigureOptions<HostEasyAuthOptionsSetup>();
                    services.AddTransient<IConfigureOptions<HostEasyAuthOptions>>(factory => new TestHostEasyAuthOptionsSetup(easyAuthSettings));
                    services.TryAddSingleton<IJobHostMiddlewarePipeline, DefaultMiddlewarePipeline>();
                    // TODO - equivalent to services.AddCors for easyauth (have to write..? or find in pkg)
                    services.TryAddEnumerable(ServiceDescriptor.Singleton<IJobHostHttpMiddleware, JobHostEasyAuthMiddleware>());
                });

            var server = new TestServer(builder); // TODO - for now need custom server bc they're only getting env vars & not config?

            var client = server.CreateClient();
            var response = await client.GetAsync(string.Empty);
            Assert.Equal(response.StatusCode.ToString(), "401");
           // Assert.Equal("test easy auth", await response.Content.ReadAsStringAsync());
        }

        // TODO - auth failure

        public class TestHostEasyAuthOptionsSetup : IConfigureOptions<HostEasyAuthOptions>
        {
            private readonly HostEasyAuthOptions _options;

            public TestHostEasyAuthOptionsSetup(HostEasyAuthOptions options)
            {
                options = _options;
            }

            public void Configure(HostEasyAuthOptions options)
            {
                options.SiteAuthClientId = _options.SiteAuthClientId;
                options.SiteAuthEnabled = _options.SiteAuthEnabled;
            }
        }
    }
}
