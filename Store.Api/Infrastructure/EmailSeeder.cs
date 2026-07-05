using Microsoft.EntityFrameworkCore;
using Store.Data;
using Store.Domain;

namespace Store.Api.Infrastructure;

/// <summary>
/// Seeds the transactional-email foundation:
///   - one default <see cref="EmailAccount"/> pointing at a placeholder localhost SMTP relay (real SMTP
///     credentials belong in configuration/user-secrets, never in a committed seed), and
///   - starter <see cref="MessageTemplate"/> rows: <c>Customer.PasswordReset</c>, <c>Customer.Welcome</c>,
///     <c>Product.BackInStock</c>, plus a customer template and an <c>.OwnerCopy</c> variant (sent to the
///     store owner, see <c>OwnerNotificationOptions</c>) for each of the four order-lifecycle events
///     (<c>Order.Placed</c>, <c>Order.Paid</c>, <c>Order.Shipped</c>, <c>Order.Cancelled</c>).
/// Templates use <c>%Token.Name%</c> placeholders (see <c>ITemplateRenderer</c>). Common order tokens:
/// <c>%Order.Number%</c>, <c>%Order.Total%</c>, <c>%Order.Status%</c>, <c>%Order.TrackingCode%</c>,
/// <c>%Customer.Name%</c>.
/// Idempotent and additive: every insert is guarded by an existence check, so it is safe to run on every
/// startup and never modifies or deletes existing rows.
/// </summary>
public static class EmailSeeder
{
    private const string DefaultAccountEmail = "no-reply@mystore.local";

    private static readonly (string Name, string Subject, string Body)[] Templates =
    [
        (
            "Customer.PasswordReset",
            "Reset your %Store.Name% password",
            """
            <p>Hello %Customer.FullName%,</p>
            <p>We received a request to reset the password for your %Store.Name% account.</p>
            <p>Use the link below to choose a new password. If you did not request this, you can safely ignore this email.</p>
            <p><a href="%Customer.PasswordResetUrl%">Reset my password</a></p>
            <p>Thanks,<br/>The %Store.Name% team</p>
            """
        ),
        (
            "Customer.Welcome",
            "Welcome to %Store.Name%, %Customer.Name%!",
            """
            <p>Hello %Customer.Name%,</p>
            <p>Thanks for creating an account with %Store.Name%. We're excited to have you with us.</p>
            <p>You can browse our catalog, track your orders, and manage your details any time from your account.</p>
            <p>Happy shopping!<br/>The %Store.Name% team</p>
            """
        ),
        (
            "Product.BackInStock",
            "%Product.Name% is back in stock at %Store.Name%",
            """
            <p>Hello,</p>
            <p>Good news — <strong>%Product.Name%</strong> is back in stock at %Store.Name%.</p>
            <p>Grab it before it sells out again.</p>
            <p>Thanks for shopping with %Store.Name%.</p>
            """
        ),
        (
            "Order.Placed",
            "Your %Store.Name% order %Order.Number% is confirmed",
            """
            <p>Hello %Customer.FullName%,</p>
            <p>Thank you for your order! We have received order <strong>%Order.Number%</strong> placed on %Order.CreatedOn%.</p>
            <p>Order total: <strong>%Order.Total%</strong></p>
            <p>We will let you know as soon as it ships.</p>
            <p>Thanks for shopping with %Store.Name%.</p>
            """
        ),
        (
            "Order.Placed.OwnerCopy",
            "New order %Order.Number% (%Order.Total%)",
            """
            <p>A new order was placed.</p>
            <p>Order: <strong>%Order.Number%</strong></p>
            <p>Customer: %Customer.Name%</p>
            <p>Total: <strong>%Order.Total%</strong></p>
            <p>Status: %Order.Status%</p>
            """
        ),
        (
            "Order.Paid",
            "We've received your payment for order %Order.Number%",
            """
            <p>Hello %Customer.Name%,</p>
            <p>Good news — we have received payment for order <strong>%Order.Number%</strong>.</p>
            <p>Order total: <strong>%Order.Total%</strong></p>
            <p>Status: %Order.Status%</p>
            <p>We will start preparing your order for shipment.</p>
            <p>Thanks for shopping with %Store.Name%.</p>
            """
        ),
        (
            "Order.Paid.OwnerCopy",
            "Payment received for order %Order.Number%",
            """
            <p>Payment was received for an order.</p>
            <p>Order: <strong>%Order.Number%</strong></p>
            <p>Customer: %Customer.Name%</p>
            <p>Total: <strong>%Order.Total%</strong></p>
            <p>Status: %Order.Status%</p>
            """
        ),
        (
            "Order.Shipped",
            "Your %Store.Name% order %Order.Number% has shipped",
            """
            <p>Hello %Customer.Name%,</p>
            <p>Your order <strong>%Order.Number%</strong> is on its way.</p>
            <p>Tracking code: <strong>%Order.TrackingCode%</strong></p>
            <p>Status: %Order.Status%</p>
            <p>Thanks for shopping with %Store.Name%.</p>
            """
        ),
        (
            "Order.Shipped.OwnerCopy",
            "Order %Order.Number% marked as shipped",
            """
            <p>An order was marked as shipped.</p>
            <p>Order: <strong>%Order.Number%</strong></p>
            <p>Customer: %Customer.Name%</p>
            <p>Tracking code: %Order.TrackingCode%</p>
            <p>Status: %Order.Status%</p>
            """
        ),
        (
            "Order.Cancelled",
            "Your %Store.Name% order %Order.Number% was cancelled",
            """
            <p>Hello %Customer.Name%,</p>
            <p>Order <strong>%Order.Number%</strong> has been cancelled.</p>
            <p>Order total: <strong>%Order.Total%</strong></p>
            <p>Status: %Order.Status%</p>
            <p>If you did not expect this, please contact us.</p>
            """
        ),
        (
            "Order.Cancelled.OwnerCopy",
            "Order %Order.Number% was cancelled",
            """
            <p>An order was cancelled.</p>
            <p>Order: <strong>%Order.Number%</strong></p>
            <p>Customer: %Customer.Name%</p>
            <p>Total: <strong>%Order.Total%</strong></p>
            <p>Status: %Order.Status%</p>
            """
        ),
        (
            "Order.Refunded",
            "Your %Store.Name% order %Order.Number% was refunded",
            """
            <p>Hello %Customer.Name%,</p>
            <p>A refund was issued for order <strong>%Order.Number%</strong>.</p>
            <p>Order total: <strong>%Order.Total%</strong></p>
            <p>Status: %Order.Status%</p>
            <p>If you have any questions, please contact us.</p>
            """
        ),
        (
            "Order.Refunded.OwnerCopy",
            "Order %Order.Number% was refunded",
            """
            <p>A refund was issued.</p>
            <p>Order: <strong>%Order.Number%</strong></p>
            <p>Customer: %Customer.Name%</p>
            <p>Total: <strong>%Order.Total%</strong></p>
            <p>Status: %Order.Status%</p>
            """
        ),
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("EmailSeeder");
        var db = sp.GetRequiredService<StoreDbContext>();

        // 1) Default email account (placeholder localhost relay — replace host/credentials before going live).
        if (!await db.EmailAccounts.AnyAsync(a => a.IsDefault, cancellationToken))
        {
            db.EmailAccounts.Add(new EmailAccount
            {
                Host = "localhost",
                Port = 25,
                EnableSsl = false,
                Username = null,
                Password = null,
                Email = DefaultAccountEmail,
                DisplayName = "MyStore",
                IsDefault = true
            });
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded default placeholder email account [{Email}].", DefaultAccountEmail);
        }

        // 2) Starter message templates (insert only the names that don't exist yet).
        var existingNames = (await db.MessageTemplates
                .Select(t => t.Name)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var (name, subject, body) in Templates)
        {
            if (existingNames.Contains(name))
            {
                continue;
            }

            db.MessageTemplates.Add(new MessageTemplate
            {
                Name = name,
                Subject = subject,
                Body = body,
                IsActive = true
            });
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} message template(s).", added);
        }
    }
}
