using Microsoft.AspNetCore.Mvc;
using MyShop.Entities.Repositories;
using MyShop.Utilities;
using System.Security.Claims;
namespace MyShop.Web.ViewComponents
{
    public class ShoppingCartViewComponent : ViewComponent
    {
        private readonly IUnitOfWork unitOfWork;
        public ShoppingCartViewComponent(IUnitOfWork unitOfWork)
        {
            this.unitOfWork = unitOfWork;
        }
        public async Task<IViewComponentResult> InvokeAsync()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
            if(claim != null)
            {
                if(HttpContext.Session.GetInt32(SD.SessionKey) != null)
                {
                    return View("Default",HttpContext.Session.GetInt32(SD.SessionKey));
                }
                else
                {
                    HttpContext.Session.SetInt32 (SD.SessionKey, unitOfWork.ShoppingCart.GetAll(
                    u => u.ApplicationUserId == claim.Value).ToList().Count());
                    return View("Default",HttpContext.Session.GetInt32(SD.SessionKey));

                }
            }
            else
            {
                HttpContext.Session.Clear();
                return View("Default",0);
            }
        }
    }
}
