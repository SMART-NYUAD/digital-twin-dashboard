using System;
using System.Threading.Tasks;

public class MQTTManager
{
    public event Action<string> OnMessageReceived;

    // Dummy person position sequences to simulate occupants moving around the lab
    private static readonly string[] dummyMessages =
    {
        "[[2.5, 3.0, 0], [5.0, 4.5, 0]]",
        "[[2.8, 3.2, 0], [5.2, 4.3, 0], [1.5, 6.0, 0]]",
        "[[3.0, 3.5, 0], [5.5, 4.0, 0]]",
        "[[2.6, 2.8, 0], [4.8, 4.8, 0], [3.5, 6.2, 0]]",
        "[[2.4, 3.1, 0], [5.1, 4.6, 0]]",
    };

    private int messageIndex = 0;
    private bool running = false;

    public async Task Initialize()
    {
        running = true;
        _ = SendDummyMessages();
        await Task.CompletedTask;
    }

    private async Task SendDummyMessages()
    {
        while (running)
        {
            await Task.Delay(2000); // emit a new position every 2 seconds
            string message = dummyMessages[messageIndex % dummyMessages.Length];
            messageIndex++;
            OnMessageReceived?.Invoke(message);
        }
    }

    public async Task Disconnect()
    {
        running = false;
        await Task.CompletedTask;
    }
}
