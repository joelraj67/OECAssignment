using MediatR;
using Microsoft.EntityFrameworkCore;
using RL.Backend.Exceptions;
using RL.Backend.Models;
using RL.Data;
using RL.Data.DataModels;

namespace RL.Backend.Commands.Handlers.Plans;

public class AssignUsersToPlanProcedureCommandHandler : IRequestHandler<AssignUsersToPlanProcedureCommand, ApiResponse<Unit>>
{
    private readonly RLContext _context;

    public AssignUsersToPlanProcedureCommandHandler(RLContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<Unit>> Handle(AssignUsersToPlanProcedureCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate request
            if (request.PlanId < 1)
                return ApiResponse<Unit>.Fail(new BadRequestException("Invalid PlanId"));
            if (request.ProcedureId < 1)
                return ApiResponse<Unit>.Fail(new BadRequestException("Invalid ProcedureId"));
            if (request.UserIds == null || !request.UserIds.Any())
                return ApiResponse<Unit>.Fail(new BadRequestException("Invalid UserIds"));

            var plan = await _context.Plans
                .Include(p => p.PlanProcedures)
                    .ThenInclude(pp => pp.PlanProcedureUsers)
                .FirstOrDefaultAsync(p => p.PlanId == request.PlanId, cancellationToken);

            if (plan == null)
                return ApiResponse<Unit>.Fail(new NotFoundException($"PlanId: {request.PlanId} not found"));

            var planProcedure = plan.PlanProcedures
                .FirstOrDefault(pp => pp.ProcedureId == request.ProcedureId);

            if (planProcedure == null)
                return ApiResponse<Unit>.Fail(new NotFoundException($"ProcedureId: {request.ProcedureId} not found"));

            var users = await _context.Users
                .Where(u => request.UserIds.Contains(u.UserId))
                .ToListAsync(cancellationToken);

            if (!users.Any())
                return ApiResponse<Unit>.Fail(new NotFoundException($"UserIds: {string.Join(", ", request.UserIds)} not found"));

            // Clear existing users and add new ones
            planProcedure.PlanProcedureUsers.Clear();
            foreach (var user in users)
            {
                planProcedure.PlanProcedureUsers.Add(new PlanProcedureUser
                {
                    User = user,
                    PlanProcedure = planProcedure,
                    UserPlanProceduresPlanId = user.UserId,
                    UserPlanProceduresProcedureId = request.ProcedureId
                });
            }
            await _context.SaveChangesAsync(cancellationToken);

            return ApiResponse<Unit>.Succeed(Unit.Value);
        }
        catch (Exception e)
        {
            return ApiResponse<Unit>.Fail(e);
        }
    }
}
