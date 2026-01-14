public class MemoryModelCommand : Singleton<MemoryModelCommand>
{
    private MemoryModel model;
    private MemoryModelCommand()
    {
        model = ModelContainer.Instance.GetModel<MemoryModel>();
    }
    public void AddMoney(int addition)
    {
        model.Money += addition;
    }
    public void EnterOnlineMode()
    {
        model.isOnlineMode.Value = true;
    }
    public void ExitOnlineMode()
    {
        model.isOnlineMode.Value = false;
    }
    public void InitMemoryModel()
    {
        model.PlayerAttr = null;
        model.Money = 0;
        model.Stage = 1;
    }
    public int GetBigStage()
    {
        return (model.Stage - 1) / 5 + 1;
    }
    public int GetSmallStage()
    {
        return model.Stage - (GetBigStage() - 1) * 5;
    }
    public string GetStageDisplayName()
    {
        switch (model.Stage)
        {
            default:
                return GetBigStage() + "-" + GetSmallStage();
        }
    }
    public string GetAreaDisplayName()
    {
        switch (GetBigStage())
        {
            case 1:
                return "教学楼";
            case 2:
                return "天台";
            case 3:
                return "储物间";
            default:
                return "第" + GetBigStage() + "区";
        }
    }

}
