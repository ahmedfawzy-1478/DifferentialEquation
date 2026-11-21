using System;
using System.Collections.Generic;
using System.Linq;

namespace DifferentialEquation.Models;

public sealed record IterationStep(
    int Iteration,
    double X,
    double Fx,
    double Dfx,
    double? Error
);

public sealed record PlotPoint(double X, double Y);

public sealed class SolverResult
{
    public bool IsSuccess { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public double Root { get; init; }
    public double FinalFx { get; init; }
    public int TotalIterations { get; init; }
    public double ConvergenceRatio { get; init; }
    public bool SatisfiesConvergenceCondition { get; init; }
    public List<IterationStep> Steps { get; init; } = new();
    public List<List<PlotPoint>> CurveSegments { get; init; } = new();
    public double XMin { get; init; }
    public double XMax { get; init; }
    public double YMin { get; init; }
    public double YMax { get; init; }
}

public static class NewtonRaphsonSolver
{
    public static SolverResult Solve(string equation, double x0, double tolerance = 1e-4, int maxIterations = 100)
    {
        if (tolerance <= 0)
        {
            return new SolverResult { IsSuccess = false, ErrorMessage = "Tolerance must be greater than zero." };
        }

        if (maxIterations < 1 || maxIterations > 1000)
        {
            return new SolverResult { IsSuccess = false, ErrorMessage = "Max iterations must be between 1 and 1000." };
        }

        try
        {
            double fx0 = MathExpressionParser.Evaluate(equation, x0);
            double dfx0 = MathExpressionParser.FirstDerivative(equation, x0);
            double d2fx0 = MathExpressionParser.SecondDerivative(equation, x0);

            if (double.IsNaN(fx0) || double.IsInfinity(fx0))
            {
                return new SolverResult { IsSuccess = false, ErrorMessage = "Function is undefined at the initial guess x0." };
            }

            double convergenceRatio = Math.Abs(dfx0) > 1e-14
                ? Math.Abs((fx0 * d2fx0) / (dfx0 * dfx0))
                : double.PositiveInfinity;

            bool satisfiesCondition = convergenceRatio < 1.0;

            List<IterationStep> steps = new()
            {
                new IterationStep(0, x0, fx0, dfx0, null)
            };

            double currentX = x0;
            double currentFx = fx0;
            double currentDfx = dfx0;
            bool converged = false;

            for (int i = 1; i <= maxIterations; i++)
            {
                if (Math.Abs(currentDfx) < 1e-14)
                {
                    return new SolverResult
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Zero derivative encountered at iteration {i - 1} (f'(x) = 0). Try a different initial guess x0."
                    };
                }

                double nextX = currentX - (currentFx / currentDfx);

                if (double.IsNaN(nextX) || double.IsInfinity(nextX))
                {
                    return new SolverResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Iteration diverged to an undefined value. Try a closer initial guess x0."
                    };
                }

                double nextFx = MathExpressionParser.Evaluate(equation, nextX);
                double nextDfx = MathExpressionParser.FirstDerivative(equation, nextX);
                double error = Math.Abs(nextX - currentX);

                steps.Add(new IterationStep(i, nextX, nextFx, nextDfx, error));

                currentX = nextX;
                currentFx = nextFx;
                currentDfx = nextDfx;

                if (error < tolerance)
                {
                    converged = true;
                    break;
                }
            }

            if (!converged)
            {
                return new SolverResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Method did not stabilize within {maxIterations} iterations. Try another initial guess x0."
                };
            }

            double minStepX = steps.Min(s => s.X);
            double maxStepX = steps.Max(s => s.X);
            double spanX = Math.Max(maxStepX - minStepX, 1.5);
            double xMin = minStepX - spanX * 0.35;
            double xMax = maxStepX + spanX * 0.35;

            List<List<PlotPoint>> segments = new();
            List<PlotPoint> currentSegment = new();
            int sampleCount = 240;
            double stepSize = (xMax - xMin) / sampleCount;

            for (int k = 0; k <= sampleCount; k++)
            {
                double sx = xMin + k * stepSize;
                try
                {
                    double sy = MathExpressionParser.Evaluate(equation, sx);
                    if (!double.IsNaN(sy) && !double.IsInfinity(sy) && Math.Abs(sy) < 1e5)
                    {
                        currentSegment.Add(new PlotPoint(sx, sy));
                    }
                    else if (currentSegment.Count > 0)
                    {
                        segments.Add(currentSegment);
                        currentSegment = new List<PlotPoint>();
                    }
                }
                catch
                {
                    if (currentSegment.Count > 0)
                    {
                        segments.Add(currentSegment);
                        currentSegment = new List<PlotPoint>();
                    }
                }
            }

            if (currentSegment.Count > 0)
            {
                segments.Add(currentSegment);
            }

            List<double> allY = segments.SelectMany(seg => seg.Select(p => p.Y))
                .Concat(steps.Select(s => s.Fx))
                .Append(0.0)
                .Where(v => !double.IsNaN(v) && !double.IsInfinity(v))
                .ToList();

            double rawYMin = allY.Min();
            double rawYMax = allY.Max();
            double spanY = Math.Max(rawYMax - rawYMin, 1.0);
            double yMin = rawYMin - spanY * 0.15;
            double yMax = rawYMax + spanY * 0.15;

            return new SolverResult
            {
                IsSuccess = true,
                Root = currentX,
                FinalFx = currentFx,
                TotalIterations = steps.Count - 1,
                ConvergenceRatio = convergenceRatio,
                SatisfiesConvergenceCondition = satisfiesCondition,
                Steps = steps,
                CurveSegments = segments,
                XMin = xMin,
                XMax = xMax,
                YMin = yMin,
                YMax = yMax
            };
        }
        catch (Exception ex)
        {
            return new SolverResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }
}