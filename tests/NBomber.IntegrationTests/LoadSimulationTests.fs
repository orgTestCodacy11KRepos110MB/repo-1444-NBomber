module Tests.LoadSimulation

open System
open Xunit
open Swensen.Unquote
open FsToolkit.ErrorHandling
open NBomber
open NBomber.Contracts
open NBomber.Domain
open NBomber.Domain.DomainTypes

[<Fact>]
let ``create should correctly calculate and order simulation within timeline`` () =
    result {
        let simulations = [
            RampingConstant(10, seconds 20)
            KeepConstant(20, seconds 50)
            KeepConstant(30, seconds 50)
            KeepConstant(1000, seconds 80)
        ]

        let! loadSimulations = LoadSimulation.create simulations
        let planedDuration = LoadSimulation.getPlanedDuration loadSimulations

        let first = loadSimulations |> List.head
        let last = loadSimulations |> List.last

        let ascendingOrderByTime = loadSimulations |> List.sortBy(fun x -> x.EndTime)

        test <@ loadSimulations.Length = simulations.Length  @>
        test <@ first.StartTime <= last.EndTime @>
        test <@ planedDuration = last.EndTime @>
        test <@ loadSimulations = ascendingOrderByTime @>
        test <@ first.PrevActorCount = 0 @>
        test <@ last.PrevActorCount = 30 @>
    }
    |> ignore

[<Fact>]
let ``calcTimeProgress should correctly calculate progress for concrete segment`` () =

    let currentTime1 = TimeSpan.Zero
    let currentTime2 = TimeSpan.FromSeconds 1
    let currentTime3 = TimeSpan.FromSeconds 5
    let currentTime4 = TimeSpan.FromSeconds 10

    let duration = TimeSpan.FromSeconds 20

    let progress1 = LoadSimulation.calcTimeProgress currentTime1 duration
    let progress2 = LoadSimulation.calcTimeProgress currentTime2 duration
    let progress3 = LoadSimulation.calcTimeProgress currentTime3 duration
    let progress4 = LoadSimulation.calcTimeProgress currentTime4 duration

    test <@ progress1 = 0 @>
    test <@ progress2 = 5 @>
    test <@ progress3 = 25 @>
    test <@ progress4 = 50 @>
