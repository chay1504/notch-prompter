require('node:http').createServer((request,response) => {
  const headers = {'Content-Type':'text/html; charset=utf-8'};
  if (request.url === '/strict') headers['Content-Security-Policy'] = "default-src 'none'; style-src 'self'; script-src 'none'; require-trusted-types-for 'script'";
  response.writeHead(200, headers);
  response.end('<!doctype html><html lang="en"><head><title>Extension test page</title><style>body{font:24px sans-serif;margin:50px;background:#f2f0ec}button{display:none!important}</style></head><body><h1>A separate page stays usable.</h1><p>NotchPrompter appears above this page when invoked.</p><input aria-label="Page input"></body></html>');
}).listen(8794,'127.0.0.1');
