using PuttySessionManager.Forms;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "PuttySessionManager_SingleInstance", out var isNew);
        if (!isNew)
        {
            MessageBox.Show("이미 실행 중입니다.", "PuTTY Session Manager",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
