# Serves the published browser build for local testing, without caching so each rebuild is picked up.
# Usage: python MMX.Web/serve.py [port] [directory]   (after: dotnet publish MMX.Web -c Release)
import http.server
import os
import sys

port = int(sys.argv[1]) if len(sys.argv) > 1 else 8080
root = sys.argv[2] if len(sys.argv) > 2 else os.path.join(
    os.path.dirname(os.path.abspath(__file__)), "bin", "Release", "net10.0", "publish", "wwwroot")


class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=root, **kwargs)

    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        super().end_headers()


http.server.ThreadingHTTPServer(("127.0.0.1", port), Handler).serve_forever()
