namespace NexSpace.Application;
public interface IStartupRegistration
{
    OperationResult GetStatus();
    OperationResult Enable(string executablePath);
    OperationResult Disable();
}
