using Geopilot.Api.Contracts;
using Geopilot.Api.Models;
using Geopilot.Pipeline;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Api.Controllers;

[TestClass]
public sealed class RecordingControllerTest
{
    private Context context;
    private RecordingController controller;

    [TestInitialize]
    public void Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();
        controller = new RecordingController(Mock.Of<ILogger<RecordingController>>(), context);
    }

    [TestCleanup]
    public void Cleanup()
    {
        context.Dispose();
    }

    [TestMethod]
    public async Task GetReturnsProtocolWithStepsInPipelineOrder()
    {
        var recording = SeedRecording();

        var response = ActionResultAssert.IsOkObjectResult<RecordingResponse>(await controller.Get(recording.JobId));

        Assert.AreEqual(recording.JobId, response.JobId);
        Assert.AreEqual("pipe_a", response.PipelineId);
        Assert.AreEqual(ProcessingState.DeliveryRestriction, response.TerminalState);
        Assert.AreEqual(ScanState.Clean, response.ScanState);

        Assert.HasCount(1, response.Files);
        Assert.AreEqual("cafe01", response.Files[0].Sha256);

        Assert.HasCount(2, response.Steps, "steps are returned in pipeline order.");
        Assert.AreEqual("matching", response.Steps[0].StepId);
        Assert.AreEqual("validation", response.Steps[1].StepId);

        var condition = response.Steps[1].Conditions.Single();
        Assert.AreEqual("validation-failed", condition.ConditionId);
        Assert.AreEqual(ConditionKind.RestrictDelivery, condition.Kind);
        Assert.IsTrue(condition.Matched);
        Assert.IsNotNull(condition.EvaluatedValues);
        Assert.AreEqual("False", condition.EvaluatedValues["validation.ValidationSuccessful"], "the stored evaluation values must reach the response as a map.");

        var artifact = response.Steps[1].Artifacts.Single();
        Assert.AreEqual(ArtifactKind.Download, artifact.Kind);
        Assert.AreEqual("error.log", artifact.OriginalFileName);

        CollectionAssert.AreEqual(
            new[]
            {
                new RecordingInputResponse("transferFile", InputOrigin.StepOutput, "matching.XtfFiles", InputKind.File, null, "data.xtf", 1024, null, true),
                new RecordingInputResponse("profile", InputOrigin.Definition, null, InputKind.Value, "DMAV", null, null, null, null),
            },
            response.Steps[1].Inputs,
            "the inputs are returned in the order they were recorded.");
    }

    [TestMethod]
    public async Task GetReturnsTheReactionEventsInTheOrderTheyHappened()
    {
        var recording = SeedRecording();

        var response = ActionResultAssert.IsOkObjectResult<RecordingResponse>(await controller.Get(recording.JobId));

        CollectionAssert.AreEqual(new int?[] { 1, 2 }, response.ReactionEvents.Select(reactionEvent => reactionEvent.Attempt).ToArray());
        var first = response.ReactionEvents[0];
        Assert.AreEqual(42, first.DeliveryId);
        Assert.AreEqual("weitergabe", first.ReactionId);
        Assert.AreEqual(ReactionKind.Transfer, first.ReactionKind);
        Assert.AreEqual(ReactionEvent.Attempt, first.Event);
        Assert.AreEqual(ReactionState.Pending, first.State);
        Assert.AreEqual(TestHelpers.Localized("The target is not reachable."), first.Message);
        Assert.IsNotNull(first.FinishedAt);
        Assert.IsNull(first.UserId);
    }

    [TestMethod]
    public async Task GetReturnsWhetherTheRunWasDeclarableAndWhyItsDeclarationFailed()
    {
        var recording = SeedRecording();
        recording.TerminalState = ProcessingState.Success;
        recording.Declarability = Declarability.Declarable;
        recording.DeclarationFailure = "No assets found.";
        context.SaveChanges();

        var response = ActionResultAssert.IsOkObjectResult<RecordingResponse>(await controller.Get(recording.JobId));

        Assert.AreEqual(Declarability.Declarable, response.Declarability);
        Assert.AreEqual("No assets found.", response.DeclarationFailure);
    }

    [TestMethod]
    public async Task GetLeavesTheDeclarabilityOfAnOlderRunOpen()
    {
        var recording = SeedRecording();

        var response = ActionResultAssert.IsOkObjectResult<RecordingResponse>(await controller.Get(recording.JobId));

        Assert.IsNull(response.Declarability, "A recording written before the protocol kept its declarability says nothing about it, which is not the same as not declarable.");
    }

    [TestMethod]
    public async Task GetReturnsNotFoundForUnknownJob()
    {
        ActionResultAssert.IsNotFound(await controller.Get(Guid.NewGuid()));
    }

    [TestMethod]
    public async Task GetDefinitionServesTheSnapshotVerbatim()
    {
        var recording = SeedRecording();

        var result = await controller.GetDefinition(recording.JobId);

        var content = result as ContentResult;
        Assert.IsNotNull(content, "the snapshot is passed through verbatim, not re-serialized.");
        Assert.AreEqual("application/json", content.ContentType);
        Assert.AreEqual("""{"pipelines": []}""", content.Content);
    }

    [TestMethod]
    public async Task GetDefinitionReturnsNotFoundForUnknownJob()
    {
        ActionResultAssert.IsNotFound(await controller.GetDefinition(Guid.NewGuid()));
    }

    private Recording SeedRecording()
    {
        var recording = new Recording
        {
            JobId = Guid.NewGuid(),
            PipelineId = "pipe_a",
            Definition = """{"pipelines": []}""",
            AppVersion = "3.0-test",
            ClientKind = ClientKind.ApiClient,
            UploadId = Guid.NewGuid(),
            UploadStorageLocation = "https://storage.example.com/uploads",
            UploadInitiatedAt = DateTime.UtcNow.AddMinutes(-10),
            StartedAt = DateTime.UtcNow.AddMinutes(-9),
            ScanState = ScanState.Clean,
            TerminalState = ProcessingState.DeliveryRestriction,
            TerminalAt = DateTime.UtcNow,
            Files = new List<RecordingFile>
            {
                new() { FileName = "data.xtf", StorageKey = "uploads/1/data.xtf", DeclaredSize = 1024, Sha256 = "cafe01" },
            },
            Steps = new List<RecordingStep>
            {
                // Inserted out of pipeline order on purpose; the response must order by Order.
                new()
                {
                    Order = 1,
                    StepId = "validation",
                    DisplayName = TestHelpers.Localized("Validierung"),
                    ProcessImplementation = "Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess",
                    State = StepState.DeliveryRestriction,
                    Conditions = new List<RecordingCondition>
                    {
                        new()
                        {
                            ConditionId = "validation-failed",
                            Phase = ConditionPhase.Post,
                            Kind = ConditionKind.RestrictDelivery,
                            Expression = "!([validation.ValidationSuccessful])",
                            Matched = true,
                            EvaluatedValues = """{"validation.ValidationSuccessful":"False"}""",
                        },
                    },
                    Artifacts = new List<RecordingArtifact>
                    {
                        new() { Kind = ArtifactKind.Download, OriginalFileName = "error.log", PersistedFileName = "validation_error.log" },
                    },
                    Inputs = new List<RecordingInput>
                    {
                        new() { Parameter = "transferFile", Origin = InputOrigin.StepOutput, Reference = "matching.XtfFiles", Kind = InputKind.File, FileName = "data.xtf", Size = 1024, FromUpload = true },
                        new() { Parameter = "profile", Origin = InputOrigin.Definition, Kind = InputKind.Value, Value = "DMAV" },
                    },
                },
                new()
                {
                    Order = 0,
                    StepId = "matching",
                    DisplayName = TestHelpers.Localized("Zuordnung"),
                    ProcessImplementation = "Geopilot.Pipeline.Processes.Matcher.XtfMatcher.XtfMatcherProcess",
                    State = StepState.Success,
                },
            },
            ReactionEvents = new List<RecordingReactionEvent>
            {
                // Inserted out of order on purpose; the response must order by when it happened.
                ReactionAttempt(2, DateTime.UtcNow.AddMinutes(-1), ReactionState.Done, "Delivered to the target."),
                ReactionAttempt(1, DateTime.UtcNow.AddMinutes(-3), ReactionState.Pending, "The target is not reachable."),
            },
        };

        context.Recordings.Add(recording);
        context.SaveChanges();
        return recording;
    }

    private static RecordingReactionEvent ReactionAttempt(int attempt, DateTime startedAt, ReactionState state, string message) => new()
    {
        DeliveryId = 42,
        ReactionId = "weitergabe",
        ReactionKind = ReactionKind.Transfer,
        Implementation = "Geopilot.Pipeline.Channels.FileDrop.FileDropChannel",
        Event = ReactionEvent.Attempt,
        Attempt = attempt,
        OccurredAt = startedAt,
        FinishedAt = startedAt.AddSeconds(5),
        State = state,
        Message = TestHelpers.Localized(message),
    };
}
