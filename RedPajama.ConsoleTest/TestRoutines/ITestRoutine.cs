using LLama;
using LLama.Abstractions;

namespace RedPajama.ConsoleTest.TestRoutines;

internal interface ITestRoutine
{
    Task Run(LLamaWeights model, IContextParams parameters);
}