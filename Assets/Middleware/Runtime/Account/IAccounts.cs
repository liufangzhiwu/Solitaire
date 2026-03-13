namespace Middleware
{
    public interface IAccounts
    {
        public string UserId { get; set; }
        public bool IsLogin { get; set; }
        void Init(float delay);
        void Login(bool isShowLoginPanel = false);
        void Logout();
        
        void VerifyPlayer();
    }
}