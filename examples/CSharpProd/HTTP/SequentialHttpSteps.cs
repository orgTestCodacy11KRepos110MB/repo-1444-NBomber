﻿using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace CSharpProd.HTTP;

public class SequentialHttpSteps
{
    public void Run()
    {
        using var httpClient = new HttpClient();

        var scenario = Scenario.Create("http_scenario", async context =>
        {
            var step1 = await Step.Run("step_1", context, async () =>
            {
                var request =
                    Http.CreateRequest("GET", "https://nbomber.com")
                        .WithHeader("Accept", "text/html")
                        .WithBody(new StringContent("{ some JSON }"));

                var response = await Http.Send(httpClient, request);

                return response;
            });

            var step2 = await Step.Run("step_2", context, async () =>
            {
                var request =
                    Http.CreateRequest("GET", "https://nbomber.com")
                        .WithHeader("Accept", "text/html")
                        .WithBody(new StringContent("{ some JSON }"));

                var response = await Http.Send(httpClient, request);

                return response;
            });

            return Response.Ok();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(Simulation.Inject(rate: 100, interval: TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30)));

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }
}
