using JetBrains.Annotations;
using LLama;
using LLama.Abstractions;

namespace RedPajama.ConsoleTest.TestRoutines;

interface ITestRoutine
{
    Task Run(LLamaWeights model, IContextParams parameters);
}