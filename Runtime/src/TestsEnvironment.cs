using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RGN.Dependencies;
using RGN.Modules.SignIn;

namespace RGN.Tests
{
    public sealed class TestsEnvironment : IDisposable
    {
        public TestsEnvironment()
        {
        }
        public void Dispose()
        {
            RGNCoreBuilder.Dispose();
        }

        public async Task SetupEnvironmentAsync(IDependencies dependencies, List<IRGNModule> modules)
        {
            foreach (IRGNModule module in modules)
            {
                RGNCoreBuilder.AddModule(module);
            }

            RGNCoreBuilder.CreateInstance(dependencies);
            TaskCompletionSource<bool> waitFirstAuthChange = new TaskCompletionSource<bool>();
            RGNCoreBuilder.I.AuthenticationChanged += OnAuthenticationChanged;
            await RGNCoreBuilder.BuildAsync();


#pragma warning disable CS4014
            Task.Delay(5000).ContinueWith(task => {
#pragma warning restore CS4014
                waitFirstAuthChange.TrySetCanceled();
            });

            await waitFirstAuthChange.Task;

            RGNCoreBuilder.I.AuthenticationChanged -= OnAuthenticationChanged;

            void OnAuthenticationChanged(EnumLoginState loginState, EnumLoginError error)
            {
                waitFirstAuthChange.TrySetResult(true);
            }
        }

        public async Task SetupTestAccountAsync(bool isAdmin)
        {
            string email = isAdmin ? "READY_RUNTIME_TESTS_ADMIN_USER_EMAIL" : "READY_RUNTIME_TESTS_NORMAL_USER_EMAIL";
            string pass = isAdmin ? "READY_RUNTIME_TESTS_ADMIN_USER_PASS" : "READY_RUNTIME_TESTS_NORMAL_USER_PASS";

            email = Environment.GetEnvironmentVariable(email);
            pass = Environment.GetEnvironmentVariable(pass);

            if (RGNCoreBuilder.I.MasterAppUser != null && RGNCoreBuilder.I.MasterAppUser.Email == email)
            {
                return;
            }

            TaskCompletionSource<bool> waitSignOut = new TaskCompletionSource<bool>();
            TaskCompletionSource<bool> waitSignIn = new TaskCompletionSource<bool>();

            RGNCoreBuilder.I.AuthenticationChanged += OnAuthenticationChanged;

            if (RGNCoreBuilder.I.IsLoggedIn)
            {
                EmailSignInModule.I.SignOut();
                await waitSignOut.Task;
            }

            EmailSignInModule.I.TryToSignIn(email, pass);
            await waitSignIn.Task;

            RGNCoreBuilder.I.AuthenticationChanged -= OnAuthenticationChanged;

            void OnAuthenticationChanged(EnumLoginState loginState, EnumLoginError error)
            {
                switch (loginState)
                {
                    case EnumLoginState.Error:
                        throw new Exception("Error while setup test account");
                    case EnumLoginState.NotLoggedIn:
                        waitSignOut.SetResult(true);
                        break;
                    case EnumLoginState.Success:
                        waitSignIn.SetResult(true);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(loginState));
                }
            }
        }
    }
}
