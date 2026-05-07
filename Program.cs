using PuttySessionManager.Forms;

// 단일 인스턴스 보장
using var mutex = new Mutex(true, "PuttySessionManager_SingleInstance", out var isNew);
if (!isNew)
{
    MessageBox.Show("이미 실행 중입니다.", "PuTTY Session Manager",
        MessageBoxButtons.OK, MessageBoxIcon.Information);
    return;
}

ApplicationConfiguration.Initialize();
Application.Run(new MainForm());
