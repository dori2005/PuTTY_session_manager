namespace PuttySessionManager.Models;

public class PuttySession
{
    /// <summary>레지스트리 서브키 이름 (URL 인코딩). putty -load 에 직접 사용.</summary>
    public string RegistryName { get; init; } = "";

    /// <summary>화면에 표시할 이름 (URL 디코딩).</summary>
    public string DisplayName { get; init; } = "";

    public override string ToString() => DisplayName;
}
