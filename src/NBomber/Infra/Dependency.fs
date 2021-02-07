module internal NBomber.Infra.Dependency

open System
open System.IO
open System.Reflection
open System.Runtime.Versioning

open Microsoft.Extensions.Configuration
open Serilog
open Serilog.Events
open ShellProgressBar

open NBomber.Configuration
open NBomber.Contracts

type IProgressBarEnv =
    abstract CreateManualProgressBar: tickCount:int -> IProgressBar
    abstract CreateAutoProgressBar: duration:TimeSpan -> IProgressBar

type IGlobalDependency =
    abstract ApplicationType: ApplicationType
    abstract NodeType: NodeType
    abstract NBomberConfig: NBomberConfig option
    abstract InfraConfig: IConfiguration option
    abstract CreateLoggerConfig: (unit -> LoggerConfiguration) option
    abstract ProgressBarEnv: IProgressBarEnv
    abstract Logger: ILogger
    abstract ReportingSinks: IReportingSink list
    abstract WorkerPlugins: IWorkerPlugin list

module Logger =

    let create (folder: string)
               (testInfo: TestInfo)
               (createConfig: (unit -> LoggerConfiguration) option)
               (configPath: IConfiguration option) =

        let attachFileLogger (config: LoggerConfiguration) =
            config.WriteTo.File(
                path = $"{folder}/{testInfo.SessionId}/nbomber-log.txt",
                outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [ThreadId:{ThreadId}] {Message:lj}{NewLine}{Exception}",
                rollingInterval = RollingInterval.Day
            )

        let attachConsoleLogger (config: LoggerConfiguration) =
            config.WriteTo.Logger(fun lc ->
                lc.WriteTo.Console()
                  .Filter.ByIncludingOnly(fun event -> event.Level = LogEventLevel.Information
                                                       || event.Level = LogEventLevel.Warning
                                                       || event.Level = LogEventLevel.Fatal)
                |> ignore
            )

        let loggerConfig =
            createConfig
            |> Option.map(fun tryGet -> tryGet())
            |> Option.defaultValue(LoggerConfiguration().MinimumLevel.Debug())
            |> fun config -> config.Enrich.WithProperty("SessionId", testInfo.SessionId)
                                   .Enrich.WithProperty("TestSuite", testInfo.TestSuite)
                                   .Enrich.WithProperty("TestName", testInfo.TestName)
                                   .Enrich.WithThreadId()
            |> attachFileLogger
            |> attachConsoleLogger

        match configPath with
        | Some path -> loggerConfig.ReadFrom.Configuration(path).CreateLogger() :> ILogger
        | None      -> loggerConfig.CreateLogger() :> ILogger

module ResourceManager =

    let readResource (name) =
        let assembly = typedefof<IGlobalDependency>.Assembly
        assembly.GetManifestResourceNames()
        |> Array.tryFind(fun x -> x.Contains name)
        |> Option.map(fun resourceName ->
            use stream = assembly.GetManifestResourceStream(resourceName)
            use reader = new StreamReader(stream)
            reader.ReadToEnd()
        )

module NodeInfo =

    let init () =

        let dotNetVersion =
            let assembly =
                if isNull(Assembly.GetEntryAssembly()) then Assembly.GetCallingAssembly()
                else Assembly.GetEntryAssembly()

            assembly.GetCustomAttribute<TargetFrameworkAttribute>().FrameworkName

        let processor = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")
        let version = typeof<ApplicationType>.Assembly.GetName().Version

        { MachineName = Environment.MachineName
          NodeType = NodeType.SingleNode
          CurrentOperation = NodeOperationType.None
          OS = Environment.OSVersion
          DotNetVersion = dotNetVersion
          Processor = if isNull processor then String.Empty else processor
          CoresCount = Environment.ProcessorCount
          NBomberVersion = sprintf "%i.%i.%i" version.Major version.Minor version.Build }

module ProgressBarEnv =

    let private options =
        ProgressBarOptions(ProgressBarOnBottom = true,
                           ForegroundColor = ConsoleColor.Yellow,
                           ForegroundColorDone = Nullable<ConsoleColor>(ConsoleColor.DarkGreen),
                           BackgroundColor = Nullable<ConsoleColor>(ConsoleColor.DarkGray),
                           BackgroundCharacter = Nullable<char>('\u2593'),
                           CollapseWhenFinished = false)

    let create () =
        { new IProgressBarEnv with
            member _.CreateManualProgressBar(ticks) =
                new ProgressBar(ticks, String.Empty, options) :> IProgressBar

            member _.CreateAutoProgressBar(duration) =
                new FixedDurationBar(duration, String.Empty, options) :> IProgressBar }

let createSessionId () =
    let date = DateTime.UtcNow.ToString("yyyy-MM-dd_HH.mm.ff")
    let guid = Guid.NewGuid().GetHashCode().ToString("x")
    date + "_" + guid

let create (folder: string) (testInfo: TestInfo)
           (appType: ApplicationType) (nodeType: NodeType)
           (context: NBomberContext) =

    let logger = Logger.create folder testInfo context.CreateLoggerConfig context.InfraConfig
    Log.Logger <- logger

    { new IGlobalDependency with
        member _.ApplicationType = appType
        member _.NodeType = nodeType
        member _.NBomberConfig = context.NBomberConfig
        member _.InfraConfig = context.InfraConfig
        member _.CreateLoggerConfig = context.CreateLoggerConfig
        member _.ProgressBarEnv = ProgressBarEnv.create()
        member _.Logger = logger
        member _.ReportingSinks = context.Reporting.Sinks
        member _.WorkerPlugins = context.WorkerPlugins }
