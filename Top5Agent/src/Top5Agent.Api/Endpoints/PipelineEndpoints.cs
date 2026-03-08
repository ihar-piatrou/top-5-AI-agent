using Top5Agent.Pipeline;

namespace Top5Agent.Api.Endpoints;

public static class PipelineEndpoints
{
    public static void MapPipelineEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/pipeline").WithTags("Pipeline");

        group.MapPost("/run", async (PipelineRunRequest req, PipelineOrchestrator orchestrator) =>
        {
            var runId = await orchestrator.RunAsync(req.Niche.ToNicheString(), req.Count, req.AutoApprove);
            return Results.Ok(new { RunId = runId });
        });
    }
}

public enum Niche
{
    Survival,
    DangerAndSafety,
    TravelAndCities,
    AnimalsAndWildlife,
    HealthAndDiseases,
    FoodAndRestaurants,
    TechnologyAndGadgets,
    CarsAndMechanics,
    OutdoorAndHiking,
    HomeImprovement,
    HistoryAndMysteries,
    ScienceAndDiscoveries,
    StrangeFacts,
    CrimeAndPrisons,
    LuxuryAndExpensiveThings,
    NatureAndDisasters,
    PsychologyAndHumanBehavior,
    ExtremePlaces,
    LifeHacksAndTricks,
    EverydayMistakes
}

public static class NicheExtensions
{
    public static string ToNicheString(this Niche niche) => niche switch
    {
        Niche.Survival => "survival",
        Niche.DangerAndSafety => "danger and safety",
        Niche.TravelAndCities => "travel and cities",
        Niche.AnimalsAndWildlife => "animals and wildlife",
        Niche.HealthAndDiseases => "health and diseases",
        Niche.FoodAndRestaurants => "food and restaurants",
        Niche.TechnologyAndGadgets => "technology and gadgets",
        Niche.CarsAndMechanics => "cars and mechanics",
        Niche.OutdoorAndHiking => "outdoor and hiking",
        Niche.HomeImprovement => "home improvement",
        Niche.HistoryAndMysteries => "history and mysteries",
        Niche.ScienceAndDiscoveries => "science and discoveries",
        Niche.StrangeFacts => "strange facts",
        Niche.CrimeAndPrisons => "crime and prisons",
        Niche.LuxuryAndExpensiveThings => "luxury and expensive things",
        Niche.NatureAndDisasters => "nature and disasters",
        Niche.PsychologyAndHumanBehavior => "psychology and human behavior",
        Niche.ExtremePlaces => "extreme places",
        Niche.LifeHacksAndTricks => "life hacks and tricks",
        Niche.EverydayMistakes => "everyday mistakes",
        _ => niche.ToString().ToLower()
    };
}

public record PipelineRunRequest(Niche Niche, int Count = 10, bool AutoApprove = false);
