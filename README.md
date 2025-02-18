# componentize-dotnet-demo
Exploring componentize-dotnet

## Pre-requisites

- [Install .NET 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- Wasmtime - `winget install BytecodeAlliance.Wasmtime`

## Hello World Demo

```bash
PS:> cd hello-world
PS:> dotnet restore
Restore complete (29.5s)

Build succeeded in 29.6s
PS:> dotnet build
Restore complete (0.4s)
  hello succeeded (76.9s) → bin\Debug\net9.0\wasi-wasm\publish\

Build succeeded in 79.7s

PS:> wasmtime bin\Debug\net9.0\wasi-wasm\native\hello.wasm
Hello, World!
```

## wasi-http-server demo

```bash
PS:> cd wasi-http
PS:> dotnet restore
Restore complete (29.5s)

Build succeeded in 29.6s
PS:> dotnet build
Restore complete (0.4s)
  hello succeeded (76.9s) → bin\Debug\net9.0\wasi-wasm\publish\

Build succeeded in 79.7s

PS:> wasmtime serve -S cli  .\bin\Debug\net9.0\wasi-wasm\native\wasi-http.wasm --addr 127.0.0.1:3000 
```

In another terminal

```bash
PS:> Invoke-RestMethod http://127.0.0.1:3000/   
Hello, World!
```
