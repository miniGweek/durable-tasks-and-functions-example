# Set up

## An API that simulates long running work

We are going to use the `DoWorkApi` to emulate an API that accepts some `work`
and takes a long time to complete. Its built using `Minimal API` of .net 6.0.

The api uses EntityFramework InMemory provider to keep an in-memory db up and running, and accepts simple `work` and tracks its status.

Execute the following `PowerShell` script to get the api up and running

`\DurableTasksAndFunctionsExamples\DoWorkApi\docker-build-run.ps1`

The API will use port 5000 by default.

## Using the API

Sample http requests to work with the API. <br/>
You can also use the `REST` plugin for `VSCode` and use the test file in `Tests\setup.http`

`### Queue work using the API

```PowerShell
    POST http://localhost:5000/todoworkitems HTTP/1.1
    content-type: application/json

    {
        "name" : "Important work 01",
        "isComplete" : false
    }
```

### List all work that is being tracked by the API

```PowerShell
    GET http://localhost:5000/todoworkitems HTTP/1.1
```

### Update work status to complete for a given work id

```PowerShell
    PUT http://localhost:5000/todoworkitems/2 HTTP/1.1
    content-type: application/json

    {
        "name" : "Important work 02 - completed",
        "isComplete" : true
    }
```

## Using the Durable functions

ToDo

## References

1. [Async Http APIs with Azure Durable Functions (and Polling Client)](https://pacodelacruzag.wordpress.com/2018/07/10/async-http-apis-with-azure-durable-functions-2/)
2. [Monitor scenario in Durable Functions - Weather watcher sample](https://docs.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-monitor?tabs=csharp)
3. [Use dependency injection in .NET Azure Functions](https://docs.microsoft.com/en-us/azure/azure-functions/functions-dotnet-dependency-injection)
