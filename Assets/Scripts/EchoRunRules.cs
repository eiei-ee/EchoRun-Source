// Capabilities belong to the frozen run mode, never to platform availability.
public sealed class EchoRunRules
{
    private static readonly EchoRunRules Legacy = new EchoRunRules(GameplayFlowMode.SixPhaseLegacy);
    private static readonly EchoRunRules Single = new EchoRunRules(GameplayFlowMode.SingleContract);
    private static readonly EchoRunRules Async = new EchoRunRules(GameplayFlowMode.AsyncChallenge);

    public GameplayFlowMode Mode { get; }
    public bool UsesSingleContractRules => Mode != GameplayFlowMode.SixPhaseLegacy;
    public bool AllowIdentityTraining => Mode != GameplayFlowMode.AsyncChallenge;
    public bool AllowGateRelearning => Mode != GameplayFlowMode.AsyncChallenge;
    public bool AllowPlayerTraining => Mode != GameplayFlowMode.AsyncChallenge;
    public bool AllowDirectorTraining => Mode != GameplayFlowMode.AsyncChallenge;
    public bool PersistRunProgress => Mode != GameplayFlowMode.AsyncChallenge;
    public bool AllowIdentityCommit => Mode == GameplayFlowMode.SingleContract;

    private EchoRunRules(GameplayFlowMode mode) { Mode = mode; }

    public static EchoRunRules For(GameplayFlowMode mode)
    {
        switch (mode)
        {
            case GameplayFlowMode.SingleContract: return Single;
            case GameplayFlowMode.AsyncChallenge: return Async;
            default: return Legacy;
        }
    }
}
