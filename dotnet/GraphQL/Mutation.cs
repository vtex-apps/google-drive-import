using DriveImport.Data;
using DriveImport.Services;
using GraphQL;
using GraphQL.Types;

namespace DriveImport.GraphQL
{
    [GraphQLMetadata("Mutation")]
    public class Mutation : ObjectGraphType<object>
    {
        public Mutation(IGoogleDriveService googleDriveService, IDriveImportRepository driveImportRepository, IVtexAPIService vtexAPIService)
        {
            Name = "Mutation";

            Field<StringGraphType>(
                "importImages",
                resolve: context =>
                {
                    return vtexAPIService.DriveImport();
                });

            Field<BooleanGraphType>(
                "revokeToken",
                arguments: new QueryArguments(
                    new QueryArgument<StringGraphType> { Name = "accountName", Description = "Account Name" }
                ),
                resolve: context =>
                {
                    bool revoked = googleDriveService.RevokeGoogleAuthorizationToken().Result;
                    if (revoked)
                    {
                        string accountName = context.GetArgument<string>("accountName");
                        driveImportRepository.SaveFolderIds(null, accountName);
                    }

                    return revoked;
                });

            Field<StringGraphType>(
                "googleAuthorize",
                resolve: context =>
                {
                    return googleDriveService.GetGoogleAuthorizationUrl();
                });

            Field<StringGraphType>(
                "createSheet",
                resolve: context =>
                {
                    return googleDriveService.CreateSheet();
                });

            Field<StringGraphType>(
                "processSheet",
                resolve: context =>
                {
                    return vtexAPIService.SheetImport();
                });

            Field<StringGraphType>(
                "addImages",
                resolve: context =>
                {
                    return googleDriveService.ClearAndAddImages();
                });
        }
    }
}