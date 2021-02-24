using DriveImport.Data;
using DriveImport.Models;
using DriveImport.Services;
using GraphQL;
using GraphQL.Types;
using System.Linq;

namespace DriveImport.GraphQL
{
    [GraphQLMetadata("Query")]
    public class Query : ObjectGraphType<object>
    {
        public Query(IGoogleDriveService googleDriveService, IDriveImportRepository driveImportRepository)
        {
            Name = "Query";

            FieldAsync<BooleanGraphType>(
                "haveToken",
                resolve: async context =>
                {
                    Token token = await googleDriveService.GetGoogleToken();
                    return token != null && !string.IsNullOrEmpty(token.RefreshToken);
                }
            );

            /// query Reviews($searchTerm: String, $from: Int, $to: Int, $orderBy: String, $status: Boolean)
            FieldAsync<StringGraphType>(
                "getOwnerEmail",
                arguments: new QueryArguments(
                    new QueryArgument<StringGraphType> { Name = "accountName", Description = "Account Name" }
                ),
                resolve: async context =>
                {
                    string email = string.Empty;
                    string accountName = context.GetArgument<string>("accountName");
                    Token token = await googleDriveService.GetGoogleToken();
                    if (token != null)
                    {
                        string newFolderId = string.Empty;
                        string imagesFolderId = string.Empty;
                        FolderIds folderIds = await driveImportRepository.LoadFolderIds(accountName);
                        if (folderIds != null)
                        {
                            newFolderId = folderIds.NewFolderId;
                            imagesFolderId = folderIds.ImagesFolderId;
                        }
                        else
                        {
                            newFolderId = await googleDriveService.FindNewFolderId(accountName);
                        }

                        ListFilesResponse listFilesResponse = await googleDriveService.ListFiles();
                        if (listFilesResponse != null)
                        {
                            var owners = listFilesResponse.Files.Where(f => f.Id.Equals(newFolderId)).Select(o => o.Owners.Distinct()).FirstOrDefault();
                            if (owners != null)
                            {
                                email = owners.Select(o => o.EmailAddress).FirstOrDefault();
                            }
                            else
                            {
                                newFolderId = await googleDriveService.FindNewFolderId(accountName);
                                owners = listFilesResponse.Files.Where(f => f.Id.Equals(newFolderId)).Select(o => o.Owners.Distinct()).FirstOrDefault();
                                if (owners != null)
                                {
                                    email = owners.Select(o => o.EmailAddress).FirstOrDefault();
                                }
                            }
                        }
                    }

                    return email;
                }
            );

            FieldAsync<StringGraphType>(
                "sheetLink",
                resolve: async context =>
                {
                    return await googleDriveService.GetSheetLink();
                }
            );
        }
    }
}