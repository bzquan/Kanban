using Kanban.Util;

namespace Kanban.Api;

/// <summary>
/// 静的結線(Model.DevProcess.Localization)用の EN 固定 ILocalization。
/// 値は WPF の Resources.resx(en)から転記(Task 4 で Kanban.Domain/Resources/locales/{en,ja,zh}.json に統一される)。
/// リクエストごとの言語は BoardCommandService が言語引数を明示的に受け取るため、ここでは静的結線しない。
/// </summary>
public class ServerLocalization : ILocalization
{
    public string BoardDefaultTitle => "New project";
    public string BoardDefaultDescription => "Project description";
    public string CardDefaultTitle => "New work item";
    public string ProcessBackLogTitle => "Backlog(Limit:∞)";
    public string ProcessRequirementTitle => "Requirement(Limit:3)";
    public string ProcessDevelopmentTitle => "Development(Limit:5)";
    public string ProcessTestTitle => "Test(Limit:3)";
    public string ProcessReleaseTitle => "Release(Limit:∞)";
    public string ProcessPreparingTitle => "Preparing";
    public string ProcessWIPTitle => "In Progress";
    public string ProcessDoneTitle => "Done";
    public string ProcessNewStepTitle => "NewWork(Limit:1)";
}
