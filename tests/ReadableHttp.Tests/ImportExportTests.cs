using ReadableHttp;
using ReadableHttp.ImportExport;

namespace ReadableHttp.Tests;

public sealed class ImportExportTests
{
    [Fact]
    public void HttpFileConverter_imports_multiple_requests()
    {
        const string content = """
        ### Get
        GET {{baseUrl}}/get
        accept: application/json

        ### Post
        POST {{baseUrl}}/post
        content-type: application/json

        {"name":"demo"}
        """;

        var requests = new HttpFileConverter().Import(content);

        Assert.Equal(2, requests.Count);
        Assert.Equal("GET", requests[0].Method);
        Assert.Equal("Post", requests[1].Name);
        Assert.Equal(ReadableBodyType.Json, requests[1].Body?.Type);
    }

    [Fact]
    public void HttpFileConverter_imports_variables_and_form_body()
    {
        const string content = """
        @baseUrl=https://api.example.test

        ### Login
        POST {{baseUrl}}/login
        content-type: application/x-www-form-urlencoded

        username=demo&password=secret
        """;

        var request = Assert.Single(new HttpFileConverter().Import(content));

        Assert.Equal("https://api.example.test", request.Variables["baseUrl"].ToTemplateValue());
        Assert.Equal(ReadableBodyType.FormUrlEncoded, request.Body?.Type);
        Assert.Equal("username", request.Body?.Form[0].Name);
    }

    [Fact]
    public void HttpFileConverter_imports_multipart_file_body()
    {
        const string content = """
        ### Upload
        POST https://api.example.test/upload
        content-type: multipart/form-data

        file=@sample.txt;type=text/plain
        note=hello
        """;

        var request = Assert.Single(new HttpFileConverter().Import(content));

        Assert.Equal(ReadableBodyType.MultipartFormData, request.Body?.Type);
        Assert.Equal(ReadableMultipartItemType.File, request.Body?.Multipart[0].Type);
        Assert.Equal("sample.txt", request.Body?.Multipart[0].FilePath);
    }

    [Fact]
    public void CurlConverter_imports_method_headers_and_body()
    {
        var request = new CurlConverter().Import(
            "curl -X POST 'https://api.example.test/users' -H 'content-type: application/json' --data-raw '{\"name\":\"demo\"}'");

        Assert.Equal("POST", request.Method);
        Assert.Equal("https://api.example.test/users", request.Url);
        Assert.Equal("content-type", request.Headers[0].Name);
        Assert.Equal("{\"name\":\"demo\"}", request.Body?.Content);
    }

    [Fact]
    public void CurlConverter_exports_request()
    {
        var command = new CurlConverter().Export(new ReadableRequest
        {
            Method = "POST",
            Url = "https://api.example.test/users",
            Headers =
            [
                new ReadableNameValue { Name = "content-type", Value = "application/json" }
            ],
            Body = new ReadableBody
            {
                Type = ReadableBodyType.Json,
                Content = "{\"name\":\"demo\"}"
            }
        });

        Assert.Contains("curl -X 'POST'", command);
        Assert.Contains("-H 'content-type: application/json'", command);
        Assert.Contains("--data-raw '{\"name\":\"demo\"}'", command);
    }

    [Fact]
    public void OpenApiConverter_imports_openapi_requests()
    {
        const string content = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Demo", "version": "1.0.0" },
          "servers": [ { "url": "https://api.example.test" } ],
          "paths": {
            "/users": {
              "get": {
                "operationId": "getUsers",
                "summary": "Get users",
                "responses": { "200": { "description": "OK" } }
              }
            }
          }
        }
        """;

        var requests = new OpenApiConverter().Import(content, ".json");

        var request = Assert.Single(requests);
        Assert.Equal("GET", request.Method);
        Assert.Equal("https://api.example.test/users", request.Url);
        Assert.Equal("Get users", request.Name);
    }

    [Fact]
    public void OpenApiConverter_exports_openapi_document()
    {
        var document = new OpenApiConverter().Export(
        [
            new ReadableRequest
            {
                Name = "Create user",
                Method = "POST",
                Url = "https://api.example.test/users",
                Body = new ReadableBody
                {
                    Type = ReadableBodyType.Json,
                    ContentType = "application/json",
                    Content = "{\"name\":\"demo\"}"
                }
            }
        ]);

        Assert.Contains("\"openapi\": \"3.0.3\"", document);
        Assert.Contains("\"/users\"", document);
        Assert.Contains("\"post\"", document);
    }

    [Fact]
    public void OpenApiConverter_imports_swagger2_body_and_security()
    {
        const string content = """
        {
          "swagger": "2.0",
          "host": "api.example.test",
          "basePath": "/v1",
          "schemes": [ "https" ],
          "securityDefinitions": {
            "apiKey": { "type": "apiKey", "name": "x-api-key", "in": "header" }
          },
          "security": [ { "apiKey": [] } ],
          "paths": {
            "/users/{id}": {
              "put": {
                "summary": "Update user",
                "parameters": [
                  { "name": "id", "in": "path", "type": "string" },
                  { "name": "payload", "in": "body", "schema": { "$ref": "#/definitions/User" } }
                ],
                "responses": { "200": { "description": "OK" } }
              }
            }
          },
          "definitions": {
            "User": {
              "type": "object",
              "properties": {
                "name": { "type": "string" },
                "age": { "type": "integer" }
              }
            }
          }
        }
        """;

        var request = Assert.Single(new OpenApiConverter().Import(content, ".json"));

        Assert.Equal("PUT", request.Method);
        Assert.Equal("https://api.example.test/v1/users/{id}", request.Url);
        Assert.Equal("id", request.PathParameters[0].Name);
        Assert.Equal(ReadableBodyType.Json, request.Body?.Type);
        Assert.Contains("\"name\": \"string\"", request.Body?.Content);
        Assert.Contains("\"age\": 0", request.Body?.Content);
        Assert.Equal(ReadableAuthType.ApiKey, request.Auth?.Type);
        Assert.Equal("x-api-key", request.Auth?.Name);
    }

    [Fact]
    public void OpenApiConverter_resolves_openapi3_refs_and_multipart()
    {
        const string content = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Demo", "version": "1.0.0" },
          "servers": [ { "url": "https://api.example.test" } ],
          "paths": {
            "/upload": {
              "post": {
                "parameters": [
                  { "$ref": "#/components/parameters/traceId" }
                ],
                "requestBody": {
                  "content": {
                    "multipart/form-data": {
                      "schema": {
                        "type": "object",
                        "properties": {
                          "file": { "type": "string", "format": "binary" },
                          "note": { "type": "string" }
                        }
                      }
                    }
                  }
                },
                "responses": { "200": { "description": "OK" } }
              }
            }
          },
          "components": {
            "parameters": {
              "traceId": { "name": "x-trace-id", "in": "header", "schema": { "type": "string" } }
            }
          }
        }
        """;

        var request = Assert.Single(new OpenApiConverter().Import(content, ".json"));

        Assert.Equal("x-trace-id", request.Headers[0].Name);
        Assert.Equal(ReadableBodyType.MultipartFormData, request.Body?.Type);
        Assert.Equal(ReadableMultipartItemType.File, request.Body?.Multipart[0].Type);
        Assert.Equal(ReadableMultipartItemType.Text, request.Body?.Multipart[1].Type);
    }
}
