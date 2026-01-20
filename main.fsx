open System
open System.Diagnostics
open System.Net.Sockets
open System.IO

module Constants =
    let HTTP_PORT = 5000

    let HTTP_VERBS = [ "GET"; "POST"; "Get"; "Post"; "get"; "post" ]

    let HTTP_ROUTES = [ "/hello"; "/notfound" ]

    let PROJ_DIRS =
        [ "ASP.NETCoreMinimalApi"
          "Express.js"
          "Giraffe.Api"
          "Giraffe.EndpointRouting.Api"
          "Go" ]

    let MAX_RETRIES = 30

    let RETRY_DELAY_MS = 1000

module ErrorHandling =
    let executeWithRetry maxRetries delayMs operation =
        let rec retry attempt =
            async {
                try
                    return! operation ()
                with ex when attempt < maxRetries ->
                    printfn "[WARN] Attempt %d failed: %s. Retrying..." attempt ex.Message
                    do! Async.Sleep(delayMs: int)
                    return! retry (attempt + 1)
            }

        retry 1

    let isPortAvailable port =
        try
            use client = new TcpClient()
            client.Connect("localhost", port)
            client.Close()
            true // Port is open/available
        with :? SocketException ->
            false // Port not available yet

let startServer (projDir: string) =
    async {
        try
            // Validate project directory exists
            let projPath = Path.Combine(__SOURCE_DIRECTORY__, projDir)

            if not (Directory.Exists projPath) then
                failwithf "Project directory not found: %s" projPath

            let procStartInfo = ProcessStartInfo()
            procStartInfo.FileName <- "dotnet"
            procStartInfo.Arguments <- sprintf "run --project %s" projDir
            procStartInfo.WorkingDirectory <- __SOURCE_DIRECTORY__
            procStartInfo.RedirectStandardOutput <- true
            procStartInfo.RedirectStandardError <- true
            procStartInfo.UseShellExecute <- false
            procStartInfo.CreateNoWindow <- true

            let proc = Process.Start procStartInfo

            if isNull proc then
                failwithf "Failed to start server process for %s" projDir

            printfn "[INFO] Server at %s starting..." projDir

            // Wait for server to be ready by checking port availability
            let! ready =
                ErrorHandling.executeWithRetry Constants.MAX_RETRIES Constants.RETRY_DELAY_MS (fun () ->
                    async {
                        if ErrorHandling.isPortAvailable Constants.HTTP_PORT then
                            printfn "[INFO] Server at %s is ready on port %d" projDir Constants.HTTP_PORT
                            return ()
                        else
                            failwith "Server not ready yet"
                    })

            return proc
        with ex ->
            printfn "[ERROR] Failed to start server %s: %s" projDir ex.Message
            return raise ex
    }

let stopServer (proc: Process) =
    async {
        try
            if not proc.HasExited then
                // Try graceful shutdown first
                printfn "[INFO] Stopping server gracefully..."
                proc.CloseMainWindow() |> ignore

                // Wait up to 5 seconds for graceful exit
                let exited = proc.WaitForExit(5000)

                if not exited then
                    printfn "[WARN] Server didn't stop gracefully, forcing shutdown..."
                    proc.Kill(true) // Kill process tree
                    proc.WaitForExit()

                printfn "[INFO] Server stopped."
            else
                printfn "[INFO] Server already exited."

            proc.Dispose()
        with ex ->
            printfn "[ERROR] Error stopping server: %s" ex.Message
    }

let makeCurlRequest (httpVerb: string) (route: string) =
    async {
        try
            let fileName = "curl"
            let procStartInfo = ProcessStartInfo()
            procStartInfo.FileName <- fileName

            let arguments =
                sprintf "-s -S -v -X %s http://localhost:%d%s" httpVerb Constants.HTTP_PORT route

            procStartInfo.Arguments <- arguments
            procStartInfo.WorkingDirectory <- __SOURCE_DIRECTORY__
            procStartInfo.RedirectStandardOutput <- true
            procStartInfo.RedirectStandardError <- true
            procStartInfo.UseShellExecute <- false
            procStartInfo.CreateNoWindow <- true

            let proc = Process.Start procStartInfo

            if isNull proc then
                failwithf "Failed to start curl process for %s %s" httpVerb route

            printfn "[INFO] Curl request with %s %s started..." httpVerb route
            let fullCmd = fileName + " " + arguments
            return (proc, fullCmd)
        with ex ->
            printfn "[ERROR] Failed to make curl request %s %s: %s" httpVerb route ex.Message
            return raise ex
    }

let writeResponse (projDir: string) (fullCmd: string) (res: string) =
    async {
        try
            let dirPath = Path.Combine(__SOURCE_DIRECTORY__, "responses")

            if not (Directory.Exists dirPath) then
                Directory.CreateDirectory dirPath |> ignore

            let horizontalLineOne = "\n@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@\n"

            let horizontalLineTwo =
                "\n\n===+===+===+===+===+===+===+===+===+===+===+===+===+===+===+===\n\n"

            let res' = horizontalLineOne + fullCmd + horizontalLineOne + res + horizontalLineTwo

            let filePath = Path.Combine(dirPath, sprintf "%s.txt" projDir)

            do! File.AppendAllTextAsync(filePath, res') |> Async.AwaitTask

            printfn "[INFO] Response written to %s" filePath
            return ()
        with ex ->
            printfn "[ERROR] Failed to write response for %s: %s" projDir ex.Message
    }

let main () =
    async {
        try
            printfn "[INFO] Starting web server testing script..."

            printfn
                "[INFO] Testing %d servers with %d HTTP verb/route combinations"
                Constants.PROJ_DIRS.Length
                (Constants.HTTP_VERBS.Length * Constants.HTTP_ROUTES.Length)

            let pairs = Seq.allPairs Constants.HTTP_VERBS Constants.HTTP_ROUTES

            for projDir in Constants.PROJ_DIRS do
                printfn "\n[INFO] ===== Testing %s =====" projDir
                let mutable serverProc: Process option = None

                try
                    let! proc = startServer projDir
                    serverProc <- Some proc

                    for httpVerb, route in pairs do
                        try
                            let! (curlProc, fullCmd) = makeCurlRequest httpVerb route

                            // Start reading streams immediately to prevent buffer deadlock
                            let outputTask = curlProc.StandardOutput.ReadToEndAsync()
                            let errorTask = curlProc.StandardError.ReadToEndAsync()

                            // Wait for curl request with timeout
                            use cts = new System.Threading.CancellationTokenSource(10000) // 10 second timeout

                            try
                                let! _ = Async.AwaitTask(curlProc.WaitForExitAsync(cts.Token))
                                ()
                            with :? System.Threading.Tasks.TaskCanceledException ->
                                printfn "[WARN] Curl request timed out"

                                if not curlProc.HasExited then
                                    curlProc.Kill()

                            // Read the streams after process completes
                            let! output = Async.AwaitTask outputTask
                            let! error = Async.AwaitTask errorTask

                            if not (String.IsNullOrWhiteSpace output) then
                                printfn "[INFO] Curl Output: %s" (output.Substring(0, min 100 output.Length))

                            // Combine verbose info (stderr) and response body (stdout)
                            let combinedResponse =
                                if String.IsNullOrWhiteSpace output then
                                    error
                                else
                                    error + "\nResponse Body:\n" + output

                            do! writeResponse projDir fullCmd combinedResponse

                            curlProc.Dispose()
                            do! Async.Sleep 500
                        with ex ->
                            printfn "[ERROR] Failed to process request %s %s: %s" httpVerb route ex.Message

                    match serverProc with
                    | Some proc -> do! stopServer proc
                    | None -> ()

                    do! Async.Sleep 2000 // Wait before starting next server
                with ex ->
                    printfn "[ERROR] Failed to test %s: %s" projDir ex.Message

                    match serverProc with
                    | Some proc ->
                        try
                            do! stopServer proc
                        with ex2 ->
                            printfn "[ERROR] Failed to stop server: %s" ex2.Message
                    | None -> ()

            printfn "\n[INFO] Testing completed successfully!"
            return 0
        with ex ->
            printfn "[ERROR] Fatal error in main: %s" ex.Message
            printfn "[ERROR] Stack trace: %s" ex.StackTrace
            return 1
    }

#time "on"
main () |> Async.RunSynchronously
#time "off"
