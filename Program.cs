using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Dapper;
using System.Text;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;


namespace ImageUpload
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            // Apply migrations and create the database
            using (var scope = host.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var dbContext = services.GetRequiredService<ApiDbContext>();
                await dbContext.Database.MigrateAsync();
            }

            await host.RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    // Specify the URL for the application to listen on
                    webBuilder.UseUrls("http://localhost:5000");
                });

        // Define the ByteArrayTypeHandler class
        public class ByteArrayTypeHandler : SqlMapper.TypeHandler<byte[]>
        {
            public override byte[] Parse(object value)
            {
                return (byte[])value;
            }

            public override void SetValue(IDbDataParameter parameter, byte[] value)
            {
                parameter.Value = value;
            }
        }
    }

    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<ApiDbContext>(options =>
            {
                options.UseSqlite("Data Source=app.db");
            });
            services.AddScoped<ApiDbContext>();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseStaticFiles();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    var dbContext = context.RequestServices.GetRequiredService<ApiDbContext>();
                    var items = dbContext.Items.ToList();

                    // Read the contents of Index.html
                    var indexHtmlPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Index.html");
                    var indexHtmlContent = await File.ReadAllTextAsync(indexHtmlPath);

                    // Append the saved items to the end of Index.html
                    var htmlBuilder = new StringBuilder(indexHtmlContent);
                    htmlBuilder.AppendLine("<div class=\"images\">");
                    foreach (var item in items)
                    {
                        var base64Image = Convert.ToBase64String(item.Image);
                        var imageSrc = $"data:image/jpeg;base64,{base64Image}";

                        htmlBuilder.AppendLine("<div class=\"in-images\">");
                        htmlBuilder.AppendLine($"<img src=\"{imageSrc}\" alt=\"{item.Name}\" id=\"{item.Name.ToLower()}\"style=\"max-height: 30vw;\">");
                        htmlBuilder.AppendLine($"<p class=\"img_text centered text-center\">{item.Name.ToUpper()}</p>");
                        htmlBuilder.AppendLine("</div>");
                    }
                    htmlBuilder.AppendLine("</div>");

                    // Send the modified Index.html as the response
                    await context.Response.WriteAsync(htmlBuilder.ToString());
                });

                endpoints.MapGet("/add-image", async context =>
                {
                    string html = $@"
                    <html>
                        <head>
                            <meta charset=""utf-8"" />
                            <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                            <title>Where do you want to travel?</title>
                            <link href=""https://fastly.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css"" rel=""stylesheet"" integrity=""sha384-9ndCyUaIbzAi2FUVXJi0CjmCapSmO7SnpJef0486qhLnuZ2cdeRhO02iuK6FUUVM"" crossorigin=""anonymous"">
                            <style>
                                .container {{
                                    font-family: Inter, system-ui, Avenir, Helvetica, Arial, sans-serif;;
                                    font-size: 1rem;
                                    font-weight: 400;
                                    line-height: 1.5;
                                    color: #212529;
                                    text-align: left; 
                                    margin: 30px 80px;
                                    }}
                            </style>
                        </head>

                        <body class=""container"">

                            <h1>Where do you want to travel?</h1>

                            <form method=""post"" action=""/add-item"" enctype=""multipart/form-data"">

                                <div class=""form-group"">
                                    <label class=""form-label"" for=""name"">Name</label>
                                    <input class=""form-control"" type=""text"" id=""name"" name=""name"" placeholder=""Enter name"" required>
                                </div>
                                <br>

                                <div class=""form-group"">
                                    <label class=""form-label"" for=""image"">Image</label><br>
                                    <input class=""form-control"" type=""file"" id=""image"" name=""image"" accept=""image/jpeg, image/png, image/gif"" required>
                                </div>
                                <br>

                                <button type=""submit"" class=""btn btn-primary"">Submit</button>
                            </form>

                        </body>
                    </html>";

                    await context.Response.WriteAsync(html);
                });

                endpoints.MapPost("/add-item", async context =>
                {
                    // Get name and image from the request form
                    var name = context.Request.Form["name"];
                    var image = context.Request.Form.Files.GetFile("image");

                    // Save the image to a byte array
                    byte[] imageData;
                    using (var stream = new MemoryStream())
                    {
                        await image.CopyToAsync(stream);
                        imageData = stream.ToArray();
                    }

                    // Insert the item and image into the database
                    var dbContext = context.RequestServices.GetRequiredService<ApiDbContext>();
                    var newItem = new Item { Name = name, Image = imageData };
                    dbContext.Items.Add(newItem);
                    await dbContext.SaveChangesAsync();

                    // Fetch all items from the database
                    var items = dbContext.Items.ToList();

                    // Read the contents of Index.html
                    var indexHtmlPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Index.html");
                    var indexHtmlContent = await File.ReadAllTextAsync(indexHtmlPath);

                    // Append the saved items to the end of Index.html
                    var htmlBuilder = new StringBuilder(indexHtmlContent);
                    // htmlBuilder.Append("<h2>Saved Items:</h2>");
                    htmlBuilder.AppendLine("<div class=\"images\">");
                    foreach (var item in items)
                    {
                        // Convert the byte array to a Base64 string
                        var base64Image = Convert.ToBase64String(item.Image);
                        var imageSrc = $"data:image/jpeg;base64,{base64Image}";

                        htmlBuilder.AppendLine("<div class=\"in-images\">");
                        htmlBuilder.AppendLine($"<img src=\"{imageSrc}\" alt=\"{item.Name}\" id=\"{item.Name.ToLower()}\"style=\"max-height: 30vw;\">");
                        htmlBuilder.AppendLine($"<p class=\"img_text centered text-center\">{item.Name.ToUpper()}</p>");
                        htmlBuilder.AppendLine("</div>");
                    }
                    htmlBuilder.AppendLine("</div>");

                    // Send the modified Index.html as the response
                    await context.Response.WriteAsync(htmlBuilder.ToString());
                });
            });
        }
    }


   public class ApiDbContext : DbContext
    {
        public DbSet<Item> Items { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=app.db");
        }

        public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure the 'Item' entity
            modelBuilder.Entity<Item>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.Image).IsRequired();
            });
        }
    }

    public class Item
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public byte[] Image { get; set; }
    }
}