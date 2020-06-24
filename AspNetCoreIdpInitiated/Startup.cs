using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sustainsys.Saml2;
using Sustainsys.Saml2.Metadata;
using Sustainsys.Saml2.Saml2P;
using Sustainsys.Saml2.WebSso;

namespace AspNetCoreIdpInitiated
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        private static IHttpContextAccessor _httpContextAccessor;

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie()
                .AddSaml2(opt =>
                {
                    opt.SPOptions.EntityId = new EntityId("https://sp.example.com");
                    opt.SPOptions.ReturnUrl = new Uri("/", UriKind.Relative);

                    // Idp using RelayState as return url.
                    opt.IdentityProviders.Add(new IdentityProvider(
                        new EntityId("https://stubidp.sustainsys.com/d661ec76-b217-4f86-9149-50a5e0a651a4/Metadata"),
                        opt.SPOptions)
                    {
                        LoadMetadata = true,
                        AllowUnsolicitedAuthnResponse = true,
                        RelayStateUsedAsReturnUrl = true
                    });

                    // Idp using a custom query string param on the Acs URL.
                    opt.IdentityProviders.Add(new IdentityProvider(
                        new EntityId("https://stubidp.sustainsys.com/d661ec76-b217-4f86-9149-50a5e0a651a5/Metadata"),
                        opt.SPOptions)
                    {
                        LoadMetadata = true,
                        AllowUnsolicitedAuthnResponse = true,
                    });

                    opt.Notifications.AcsCommandResultCreated = AcsCommandResultCreated;
                });

            services.AddHttpContextAccessor();
        }

        private void AcsCommandResultCreated(CommandResult commandResult, Saml2Response saml2Response)
        {
            var httpContext = _httpContextAccessor.HttpContext;

            var target = httpContext.Request.Query["target"].SingleOrDefault();

            if(!string.IsNullOrEmpty(target))
            {
                // Avoid an open redirect. Note that on a shared host with multiple applications running
                // in different subdirectories this check is not enough. 
                var targetUri = new Uri(target, UriKind.Relative);

                // A protocol relative url is relative, but can still redirect to another host. Block it.
                if(target.StartsWith("//"))
                {
                    throw new InvalidOperationException("Protocol relative URLs are not allowed.");
                }

                commandResult.Location = targetUri;
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHttpContextAccessor httpContextAccessor)
        {
            // The HttpContextAccessor is a singleton, so it can be assigned to a static variable. Not
            // exactly elegant, but it works.
            _httpContextAccessor = httpContextAccessor;

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseAuthentication();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
