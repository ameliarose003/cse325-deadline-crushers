using Microsoft.AspNetCore.Components;
using System.Threading.Tasks;
using Momentum.Models;
using Momentum.Services;

namespace Momentum.Components.Pages.Account;

public partial class Account
{
    [Inject]
    protected IAuthService AuthService { get; set; } = null!;

    [Inject]
    protected NavigationManager NavigationManager { get; set; } = null!;

    protected bool isAuthenticated;
    protected string email = string.Empty;
    protected string firstName = string.Empty;
    protected string lastName = string.Empty;
    protected string newPassword = string.Empty;
    protected string confirmPassword = string.Empty;

    protected string? successMessage;
    protected string? errorMessage;

    protected override async Task OnInitializedAsync()
    {
        isAuthenticated = await AuthService.IsAuthenticatedAsync();
        if (!isAuthenticated)
        {
            NavigationManager.NavigateTo("/login");
            return;
        }

        var user = await AuthService.GetCurrentUserAsync();
        if (user != null)
        {
            email = user.Email;
            firstName = user.FirstName;
            lastName = user.LastName;
        }
    }

    protected async Task HandleSubmit()
    {
        successMessage = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            errorMessage = "First name and last name are required.";
            return;
        }

        if (!string.IsNullOrEmpty(newPassword))
        {
            if (newPassword.Length < 8)
            {
                errorMessage = "Password must be at least 8 characters.";
                return;
            }

            if (newPassword != confirmPassword)
            {
                errorMessage = "Passwords do not match.";
                return;
            }
        }

        var success = await AuthService.UpdateCurrentUserAsync(firstName, lastName, string.IsNullOrEmpty(newPassword) ? null : newPassword);
        if (success)
        {
            successMessage = "Account updated successfully!";
            newPassword = string.Empty;
            confirmPassword = string.Empty;
        }
        else
        {
            errorMessage = "Failed to update account. Please try again.";
        }
    }
}
