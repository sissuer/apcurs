﻿using apcurs.Models;
using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;

namespace apcurs.Controllers
{

    public class accountController : Controller
    {
        
        DbDataContext db = new DbDataContext();

        public static string GetClientIp()
        {

            var ipAddress = string.Empty;
            if (System.Web.HttpContext.Current.Request.ServerVariables["HTTP_X_FORWARDED_FOR"] != null)
                ipAddress = System.Web.HttpContext.Current.Request.ServerVariables["HTTP_X_FORWARDED_FOR"].ToString();
            else if (System.Web.HttpContext.Current.Request.ServerVariables["HTTP_CLIENT_IP"] != null && System.Web.HttpContext.Current.Request.ServerVariables["HTTP_CLIENT_IP"].Length != 0)
                ipAddress = System.Web.HttpContext.Current.Request.ServerVariables["HTTP_CLIENT_IP"];
            else if (System.Web.HttpContext.Current.Request.UserHostAddress.Length != 0)
                ipAddress = System.Web.HttpContext.Current.Request.UserHostName;
            return ipAddress;
        }

        public string GetErrorMessage(Exception ex)
        {
            StringBuilder errorMessage = new StringBuilder(200);

            errorMessage.AppendFormat("<div class=\"validation-summary-error\" title=\"Server Error\">{0}</div>", ex.GetBaseException().Message);
            Response.StatusCode = (int)HttpStatusCode.InternalServerError;  //jquerymodal'a 500 hata kodunu gönderiyor

            return errorMessage.ToString();
        }


        [AllowAnonymous]
        public ActionResult login()
        {

            LoginViewModel log = new LoginViewModel();

            return View(log);
        }
        //private HttpCookie CreateStudentCookie()
        //{
        //    HttpCookie StudentCookies = new HttpCookie("StudentCookies");
        //    StudentCookies.Value = "hallo";
        //    StudentCookies.Expires = DateTime.Now.AddHours(1);
        //    return StudentCookies;
        //}


        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult login(LoginViewModel model)
        {

            if (!ModelState.IsValid)
            {
                return View();
            }
        
            var encryptedPassword = FormsAuthentication.HashPasswordForStoringInConfigFile(model.Password, "md5");

            var user= db.Users.Where(a => a.UserName == model.UserName || a.Email==model.UserName).FirstOrDefault();

            if (user.Password==encryptedPassword)
            {
                if (user.ConfirmedEmail == true)
                {
                    Session["user"] = user;
                    Session["userName"] = user.UserName;
                    Session["userid"] = user.id;
                    
                    return RedirectToAction("Loggedin");
                }
                else
                {
                    ModelState.AddModelError("", "Lütfen Email Adresinizi Onaylayın");
                }
            }

            
            ModelState.AddModelError("", "Geçersiz Kullanıcı Adı veya Şifre");
            ModelState.Remove("Password");
            return View(model);
        }
        public ActionResult Loggedin()
        {
            if (Session["user"] != null)
            {
                return RedirectToAction("Index", "Home");
            }
            else
            {
                return RedirectToAction("Login");
            }
        }
        public ActionResult LogOut()
        {
            FormsAuthentication.SignOut();
            Session.Abandon();
            return RedirectToAction("index", "Home");
        }



        [AllowAnonymous]
        public ActionResult userRegister()
        {
            return View();
        }


        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult userRegister(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);

            }
            var users = db.Users.ToList();
            bool IsExistUserName = false;
            bool IsExistEmail = false;

            try
            {

                foreach (var item in users)
                {
                    if (model.UserName == item.UserName)
                    {
                        IsExistUserName = true;
                    }
                    if (model.Email == item.Email)
                    {
                        IsExistEmail = true;
                    }
                }
                if (IsExistUserName == false)
                {

                    if (IsExistEmail == false)
                    {
                        User user = new Models.User();
                        user.UserName = model.UserName;
                        user.Password = FormsAuthentication.HashPasswordForStoringInConfigFile(model.Password, "md5");
                        user.CreatedDate = DateTime.Now;
                        user.Status = true;
                        user.Email = model.Email;
                        user.ConfirmedEmail = false;
                        user.Ip = GetClientIp();

                        db.Users.InsertOnSubmit(user);
                        db.SubmitChanges();



                        MailMessage m = new MailMessage(
                                new MailAddress("omar_besiktas@hotmail.com", "Web Registration"),
                                new MailAddress(user.Email));
                        m.Subject = "Email Doğrulama";
                        m.Body = string.Format("Sevgili {0}<BR/>Kaydınız için Teşekkür Ederiz, Kaydınızı Tamamlamak için Lütfen Aşağıdaki Linki Tıklayınız: <a href=\"{1}\" title=\"Kullanıcı EPosta Onaylama\">{1}</a>", user.UserName, Url.Action("ConfirmEmail", "Account", new { Token = user.UserName, Email = user.Email }, Request.Url.Scheme));
                        m.IsBodyHtml = true;

                        using (var smtp = new SmtpClient())
                        {
                            var credential = new NetworkCredential
                            {
                                UserName = "omar_besiktas@hotmail.com",
                                Password = "'ail'-5i55u3r-"
                            };
                            smtp.Credentials = credential;
                            smtp.Host = "smtp.live.com";
                            smtp.Port = 587;
                            smtp.EnableSsl = true;
                            smtp.Send(m);
                        }


                        return RedirectToAction("Confirm", "Account", new { Email = user.Email });
                    }
                    else
                    {
                        return RedirectToAction("ResetPassword", "Account", new { ExistedUser = true });
                    }
                }
                else
                {
                    ModelState.AddModelError("", "Lütfen Farklı Bir Kullanıcı Adı Belirleyiniz");
                    return View(model);
                }
            }
            catch (Exception ex)
            {

                return Content(GetErrorMessage(ex));

            }
        }

        [AllowAnonymous]
        public ActionResult Confirm(string Email)
        {
            ViewBag.Email = Email;
            return View();
        }
        // GET: /Account/ConfirmEmail
        [AllowAnonymous]
        public ActionResult ConfirmEmail(string Token, string Email)
        {
            var user = db.Users.Where(a => a.UserName == Token).FirstOrDefault();
            if (user != null)
            {
                if (user.Email == Email)
                {
                    user.ConfirmedEmail = true;
                    UpdateModel(user);
                    db.SubmitChanges();
                    return RedirectToAction("Index", "Home", new { ConfirmedEmail = user.Email });
                }
                else
                {
                    return RedirectToAction("Confirm", "Account", new { Email = user.Email });
                }
            }
            else
            {
                return RedirectToAction("Confirm", "Account", new { Email = "" });
            }

        }

        public ActionResult ResetPassword(bool ExistedUser)
        {
            if (ExistedUser == true)
            {
                ViewBag.ExistedUser = true;
            }

            return View();
        }
        [HttpPost]
        [AllowAnonymous]
        public ActionResult ResetPassword(string email)
        {
           
            if (!ModelState.IsValid)
            {
                return View();
            }
            User model = new Models.User();
            model = db.Users.Where(a => a.Email == email).FirstOrDefault();


            if (model!=null)
            {

                string resetLink = Url.Action("LostPassword", "Account", new { rt = model.UserName }, Request.Url.Scheme);

                string subject = "Parola Sıfırla";
                string body = "Sıfırlama Linki: " + resetLink;
                MailMessage message = new MailMessage(
              new MailAddress("omar_besiktas@hotmail.com", "Parola Sıfırlama"),
              new MailAddress(model.Email));

                message.Subject = subject;
                message.Body = body;

                using (var smtp = new SmtpClient())
                {
                    try
                    {
                        var credential = new NetworkCredential
                        {
                            UserName = "omar_besiktas@hotmail.com",
                            Password = "'ail'-5i55u3r-"
                        };
                        smtp.Credentials = credential;
                        smtp.Host = "smtp.live.com";
                        smtp.Port = 587;
                        smtp.EnableSsl = true;
                        smtp.Send(message);
                    }
                    catch (Exception e)
                    {
                        ModelState.AddModelError("", "Email gönderirken Hata: " + e.Message);
                    }
                }


                return RedirectToAction("RecoveryMailConfirm", new { Email = model.Email }); 
            }
            else
            {
                ModelState.AddModelError("", "Girdiğiniz Eposta Sistemde Kayıtlı Değil Lütfen Kayıt Olunuz.");
                return View();
            }
        }

        [AllowAnonymous]
        public ActionResult RecoveryMailConfirm(string Email)
        {
            ViewBag.Email = Email;
            return View();
        }
        [AllowAnonymous]
        public ActionResult LostPassword(string rt)
        {
            LostPasswordModel model = new LostPasswordModel();
            model.ReturnToken = rt;
            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult LostPassword(LostPasswordModel model)
        {
            bool tr = false;
            if (ModelState.IsValid)
            {

                User user = db.Users.Where(a => a.UserName == model.ReturnToken).FirstOrDefault();

                user.Password = FormsAuthentication.HashPasswordForStoringInConfigFile(model.Password, "md5");               
                UpdateModel(user.Password);
                db.SubmitChanges();
                tr = true;

                if (tr == true)
                {
                    ViewBag.Message = "Şifreniz Başarıyla Değiştirildi.Yeni Şifrenizle Giriş Yapabilirsiniz.";
                }

                else
                {
                    ViewBag.Message = "Parola Sıfırlamada Hata Oluştu";
                }
            }
            return View();
        }





        ////
        //// POST: /Account/Disassociate
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<ActionResult> Disassociate(string loginProvider, string providerKey)
        //{
        //    ManageMessageId? message = null;
        //    IdentityResult result = await UserManager.RemoveLoginAsync(User.Identity.GetUserId(), new UserLoginInfo(loginProvider, providerKey));
        //    if (result.Succeeded)
        //    {
        //        message = ManageMessageId.RemoveLoginSuccess;
        //    }
        //    else
        //    {
        //        message = ManageMessageId.Error;
        //    }
        //    return RedirectToAction("Manage", new { Message = message });
        //}

        ////
        //// GET: /Account/Manage
        //public ActionResult Manage(ManageMessageId? message)
        //{
        //    ViewBag.StatusMessage =
        //        message == ManageMessageId.ChangePasswordSuccess ? "Your password has been changed."
        //        : message == ManageMessageId.SetPasswordSuccess ? "Your password has been set."
        //        : message == ManageMessageId.RemoveLoginSuccess ? "The external login was removed."
        //        : message == ManageMessageId.Error ? "An error has occurred."
        //        : "";
        //    ViewBag.HasLocalPassword = HasPassword();
        //    ViewBag.ReturnUrl = Url.Action("Manage");
        //    return View();
        //}

        ////
        //// POST: /Account/Manage
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<ActionResult> Manage(ManageUserViewModel model)
        //{
        //    bool hasPassword = HasPassword();
        //    ViewBag.HasLocalPassword = hasPassword;
        //    ViewBag.ReturnUrl = Url.Action("Manage");
        //    if (hasPassword)
        //    {
        //        if (ModelState.IsValid)
        //        {
        //            IdentityResult result = await UserManager.ChangePasswordAsync(User.Identity.GetUserId(), model.OldPassword, model.NewPassword);
        //            if (result.Succeeded)
        //            {
        //                return RedirectToAction("Manage", new { Message = ManageMessageId.ChangePasswordSuccess });
        //            }
        //            else
        //            {
        //                AddErrors(result);
        //            }
        //        }
        //    }
        //    else
        //    {
        //        // User does not have a password so remove any validation errors caused by a missing OldPassword field
        //        ModelState state = ModelState["OldPassword"];
        //        if (state != null)
        //        {
        //            state.Errors.Clear();
        //        }

        //        if (ModelState.IsValid)
        //        {
        //            IdentityResult result = await UserManager.AddPasswordAsync(User.Identity.GetUserId(), model.NewPassword);
        //            if (result.Succeeded)
        //            {
        //                return RedirectToAction("Manage", new { Message = ManageMessageId.SetPasswordSuccess });
        //            }
        //            else
        //            {
        //                AddErrors(result);
        //            }
        //        }
        //    }

        //    // If we got this far, something failed, redisplay form
        //    return View(model);
        //}

        ////
        //// POST: /Account/ExternalLogin
        //[HttpPost]
        //[AllowAnonymous]
        //[ValidateAntiForgeryToken]
        //public ActionResult ExternalLogin(string provider, string returnUrl)
        //{
        //    // Request a redirect to the external login provider
        //    return new ChallengeResult(provider, Url.Action("ExternalLoginCallback", "Account", new { ReturnUrl = returnUrl }));
        //}

        //[AllowAnonymous]
        //public async Task<ActionResult> ExternalLoginCallback(string returnUrl)
        //{
        //    var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync();
        //    if (loginInfo == null)
        //    {
        //        return RedirectToAction("Login");
        //    }

        //    // Sign in the user with this external login provider if the user already has a login
        //    var user = await UserManager.FindAsync(loginInfo.Login);
        //    if (user != null)
        //    {
        //        await SignInAsync(user, isPersistent: false);
        //        return RedirectToLocal(returnUrl);
        //    }
        //    else
        //    {
        //        // If the user does not have an account, then prompt the user to create an account
        //        ViewBag.ReturnUrl = returnUrl;
        //        ViewBag.LoginProvider = loginInfo.Login.LoginProvider;
        //        return View("ExternalLoginConfirmation", new ExternalLoginConfirmationViewModel { UserName = loginInfo.DefaultUserName });
        //    }
        //}



        ////
        //// POST: /Account/LinkLogin
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public ActionResult LinkLogin(string provider)
        //{
        //    // Request a redirect to the external login provider to link a login for the current user
        //    return new ChallengeResult(provider, Url.Action("LinkLoginCallback", "Account"), User.Identity.GetUserId());
        //}


        //public async Task<ActionResult> LinkLoginCallback()
        //{
        //    var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync(XsrfKey, User.Identity.GetUserId());
        //    if (loginInfo == null)
        //    {
        //        return RedirectToAction("Manage", new { Message = ManageMessageId.Error });
        //    }
        //    var result = await UserManager.AddLoginAsync(User.Identity.GetUserId(), loginInfo.Login);
        //    if (result.Succeeded)
        //    {
        //        return RedirectToAction("Manage");
        //    }
        //    return RedirectToAction("Manage", new { Message = ManageMessageId.Error });
        //}


        ////
        //// POST: /Account/ExternalLoginConfirmation
        //[HttpPost]
        //[AllowAnonymous]
        //[ValidateAntiForgeryToken]
        //public async Task<ActionResult> ExternalLoginConfirmation(ExternalLoginConfirmationViewModel model, string returnUrl)
        //{
        //    if (User.Identity.IsAuthenticated)
        //    {
        //        return RedirectToAction("Manage");
        //    }

        //    if (ModelState.IsValid)
        //    {
        //        // Get the information about the user from the external login provider

        //        var info = await AuthenticationManager.GetExternalLoginInfoAsync();
        //        if (info == null)
        //        {
        //            return View("ExternalLoginFailure");
        //        }
        //        var user = new ApplicationUser() { UserName = model.UserName };
        //        var result = await UserManager.CreateAsync(user);
        //        if (result.Succeeded)
        //        {
        //            result = await UserManager.AddLoginAsync(user.Id, info.Login);
        //            if (result.Succeeded)
        //            {
        //                await SignInAsync(user, isPersistent: false);
        //                return RedirectToLocal(returnUrl);
        //            }
        //        }
        //        AddErrors(result);
        //    }

        //    ViewBag.ReturnUrl = returnUrl;
        //    return View(model);
        //}

        ////
        //// POST: /Account/LogOff
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public ActionResult LogOff()
        //{
        //    AuthenticationManager.SignOut();
        //    return RedirectToAction("Index", "Home");
        //}


        ////
        //// GET: /Account/ExternalLoginFailure
        //[AllowAnonymous]
        //public ActionResult ExternalLoginFailure()
        //{
        //    return View();
        //}

        //[ChildActionOnly]
        //public ActionResult RemoveAccountList()
        //{
        //    var linkedAccounts = UserManager.GetLogins(User.Identity.GetUserId());
        //    ViewBag.ShowRemoveButton = HasPassword() || linkedAccounts.Count > 1;
        //    return (ActionResult)PartialView("_RemoveAccountPartial", linkedAccounts);
        //}
        //private const string XsrfKey = "XsrfId";

        //private IAuthenticationManager AuthenticationManager
        //{
        //    get
        //    {

        //        return HttpContext.GetOwinContext().Authentication;
        //    }
        //}

        //private async Task SignInAsync(ApplicationUser user, bool isPersistent)
        //{
        //    AuthenticationManager.SignOut(DefaultAuthenticationTypes.ExternalCookie);
        //    var identity = await UserManager.CreateIdentityAsync(user, DefaultAuthenticationTypes.ApplicationCookie);
        //    AuthenticationManager.SignIn(new AuthenticationProperties() { IsPersistent = isPersistent }, identity);
        //}

        //protected override void Dispose(bool disposing)
        //{
        //    if (disposing && UserManager != null)
        //    {
        //        UserManager.Dispose();
        //        UserManager = null;
        //    }
        //    base.Dispose(disposing);
        //}

        //#region Helpers
        //// Used for XSRF protection when adding external logins
        //private string message;

        //private void AddErrors(IdentityResult result)
        //{
        //    foreach (var error in result.Errors)
        //    {
        //        ModelState.AddModelError("", error);
        //    }
        //}

        //private bool HasPassword()
        //{
        //    var user = UserManager.FindById(User.Identity.GetUserId());
        //    if (user != null)
        //    {
        //        return user.PasswordHash != null;
        //    }
        //    return false;
        //}

        //public enum ManageMessageId
        //{
        //    ChangePasswordSuccess,
        //    SetPasswordSuccess,
        //    RemoveLoginSuccess,
        //    Error
        //}

        //private ActionResult RedirectToLocal(string returnUrl)
        //{
        //    if (Url.IsLocalUrl(returnUrl))
        //    {
        //        return Redirect(returnUrl);
        //    }
        //    else
        //    {
        //        return RedirectToAction("Index", "Home");
        //    }
        //}

        //private class ChallengeResult : HttpUnauthorizedResult
        //{
        //    public ChallengeResult(string provider, string redirectUri) : this(provider, redirectUri, null)
        //    {
        //    }

        //    public ChallengeResult(string provider, string redirectUri, string userId)
        //    {
        //        LoginProvider = provider;
        //        RedirectUri = redirectUri;
        //        UserId = userId;
        //    }

        //    public string LoginProvider { get; set; }
        //    public string RedirectUri { get; set; }
        //    public string UserId { get; set; }


        //}
        ////private async Task SignInAsync(ApplicationUser user, bool isPersistent)
        ////{
        ////    AuthenticationManager.SignOut(DefaultAuthenticationTypes.ExternalCookie);
        ////    var identity = await UserManager.CreateIdentityAsync(user, DefaultAuthenticationTypes.ApplicationCookie);
        ////    AuthenticationManager.SignIn(new AuthenticationProperties() { IsPersistent = isPersistent }, identity);
        ////}
        //#endregion




























        //[HttpPost]    
        //public ActionResult UserRegister(RegisterViewModel user)
        //{
        //    if (!ModelState.IsValid)
        //    {
        //        return View(user);
        //    }
        //    try
        //    {









        //            var response = Request["g-recaptcha-response"];
        //        const string secret = "6LdKtS8UAAAAAGf-ssJK7bL2RnaERAu7mj4qkJwr";
        //        var client = new WebClient();
        //        var reply =
        //            client.DownloadString(
        //                string.Format("https://www.google.com/recaptcha/api/siteverify?secret={0}&response={1}", secret, response));

        //        var captchaResponse = JsonConvert.DeserializeObject<CaptchaResponse>(reply);

        //        if (!captchaResponse.Success)
        //            TempData["Message"] = "Lütfen güvenliği doğrulayınız.";
        //        else
        //            TempData["Message"] = "Güvenlik başarıyla doğrulanmıştır.";

        //        User u = new User();
        //        u.NameSurname = user.NameSurname;
        //        u.Email = user.Email;
        //        u.Password = FormsAuthentication.HashPasswordForStoringInConfigFile(user.Password, "md5");
        //        u.CreatedDate = DateTime.Now;


        //        db.GetTable<User>().InsertOnSubmit(u);
        //        db.SubmitChanges();
        //        return RedirectToAction("Index");

        //    }
        //    catch (Exception ex)
        //    {

        //        return Content(GetErrorMessage(ex));
        //    }

        //}


    }
}