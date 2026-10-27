using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Admin;

public sealed class AdminService(AdminDb db)
{
    public async Task<TenantSetting> SetTenantSettingAsync(
        Guid tenantId, string key, string valueJson, string actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        using var value = JsonDocument.Parse(valueJson);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var setting = await db.TenantSettings.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.Key == key, cancellationToken);
        if (setting is null)
        {
            setting = new TenantSetting
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = key.Trim(),
                ValueJson = valueJson,
                UpdatedBy = actorId.Trim()
            };
            db.TenantSettings.Add(setting);
        }
        else
        {
            setting.ValueJson = valueJson;
            setting.UpdatedAt = DateTimeOffset.UtcNow;
            setting.UpdatedBy = actorId.Trim();
        }
        AddAudit(actorId, "tenant_setting.updated", "tenant_setting", setting.Id.ToString(), key);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return setting;
    }

    public async Task<OperationalControl> SetOperationalControlAsync(
        string controlKey, bool isEnabled, string? reason, string actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(controlKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        if (!isEnabled && string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required when disabling an operational control.", nameof(reason));
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var control = await db.OperationalControls.SingleOrDefaultAsync(x => x.ControlKey == controlKey, cancellationToken);
        if (control is null)
        {
            control = new OperationalControl
            {
                Id = Guid.NewGuid(),
                ControlKey = controlKey.Trim(),
                IsEnabled = isEnabled,
                Reason = reason?.Trim(),
                UpdatedBy = actorId.Trim()
            };
            db.OperationalControls.Add(control);
        }
        else
        {
            control.IsEnabled = isEnabled;
            control.Reason = reason?.Trim();
            control.UpdatedAt = DateTimeOffset.UtcNow;
            control.UpdatedBy = actorId.Trim();
        }
        AddAudit(actorId, "operational_control.updated", "operational_control", control.Id.ToString(),
            JsonSerializer.Serialize(new { control.ControlKey, control.IsEnabled, control.Reason }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return control;
    }

    public Task<List<OperationalControl>> GetOperationalControlsAsync(CancellationToken cancellationToken = default) =>
        db.OperationalControls.AsNoTracking().OrderBy(x => x.ControlKey).ToListAsync(cancellationToken);

    private void AddAudit(string actorId, string action, string targetType, string? targetId, string? details) =>
        db.AuditEvents.Add(new AdminAuditEvent
        {
            Id = Guid.NewGuid(),
            ActorId = actorId.Trim(),
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            DetailsJson = JsonSerializer.Serialize(new { details })
        });
}
