using ISP.Ticketing.ML.Training;
using ISP.Ticketing.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISP.Ticketing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Supervisor")]
public class MlController(
    MlTrainingService trainingService,
    IClassificationService classifier, MlComparisonService comparisonService) : ControllerBase
{
    public sealed class ClassificationTestRequest
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
    }

    [HttpPost("classify-test")]
    public async Task<IActionResult> ClassifyTest(
        [FromBody] ClassificationTestRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title) ||
            string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest(new { message = "Title and description are required." });
        }

        try
        {
            var result = await classifier.ClassifyAsync(
                request.Title.Trim(),
                request.Description.Trim(),
                ct);

            if (!result.IsSuccess || result.Value is null)
                return BadRequest(new { message = result.Error ?? "Classification failed." });

            var value = result.Value;

            return Ok(new
            {
                categoryId = value.CategoryId,
                category = value.CategoryName,
                priority = value.Priority.ToString(),
                slaHours = value.SlaHours,
                confidence = value.Confidence,
                confidencePercent = Math.Round(value.Confidence * 100m, 2),
                modelVersion = value.ModelVersion,
                processingTimeMs = value.ProcessingTimeMs
            });
        }
        catch (Exception ex)
        {
            return Problem(
                title: "ML classification failed",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    [HttpGet("compare")]
    public IActionResult Compare()
    {
        try { return Ok(comparisonService.Compare()); }
        catch (Exception ex) { return Problem(title: "ML comparison failed", detail: ex.Message, statusCode: 500); }
    }

    [HttpGet("training-report")]
    public ActionResult<MlTrainingReport> GetTrainingReport()
    {
        try
        {
            return Ok(trainingService.TrainAndEvaluate());
        }
        catch (FileNotFoundException ex)
        {
            return Problem(title: "ML dataset not found", detail: ex.Message, statusCode: 500);
        }
        catch (Exception ex)
        {
            return Problem(title: "ML training failed", detail: ex.Message, statusCode: 500);
        }
    }
}
