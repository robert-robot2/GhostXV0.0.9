using System.Net;

namespace GhostShell
{
    public static class GhostUIHost
    {
        private static HttpListener? _listener;
        private static string _wwwroot = string.Empty;

        public static void Start()
        {
            _wwwroot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");

            _listener = new HttpListener();
            _listener.Prefixes.Add("http://localhost:5050/");
            _listener.Start();

            _ = Task.Run(ListenLoop);
        }

        private static async Task ListenLoop()
        {
            while (_listener is { IsListening: true })
            {
                try
                {
                    var ctx = await _listener.GetContextAsync();
                    HandleRequest(ctx);
                }
                catch
                {
                    break;
                }
            }
        }

        private static void HandleRequest(HttpListenerContext ctx)
        {
            try
            {
                var urlPath = ctx.Request.Url?.AbsolutePath ?? "/";
                var relativePath = Uri.UnescapeDataString(urlPath).TrimStart('/');

                string filePath;

                if (string.IsNullOrEmpty(relativePath))
                {
                    filePath = Path.Combine(_wwwroot, "index.html");
                }
                else
                {
                    filePath = Path.Combine(_wwwroot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                }

                if (!File.Exists(filePath))
                {
                    ctx.Response.StatusCode = 404;
                    return;
                }

                var bytes = File.ReadAllBytes(filePath);
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = GetContentType(filePath);
                ctx.Response.ContentLength64 = bytes.Length;
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            finally
            {
                try { ctx.Response.OutputStream.Close(); } catch { }
            }
        }

        private static string GetContentType(string path)
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".html" => "text/html; charset=utf-8",
                ".js" => "application/javascript; charset=utf-8",
                ".wasm" => "application/wasm",
                ".css" => "text/css; charset=utf-8",
                ".json" => "application/json; charset=utf-8",
                ".dll" => "application/octet-stream",
                ".png" => "image/png",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream"
            };
        }

        public static void Stop()
        {
            _listener?.Stop();
            _listener?.Close();
        }
    }
}