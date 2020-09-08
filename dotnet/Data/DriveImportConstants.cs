using System;
using System.Collections.Generic;
using System.Text;

namespace DriveImport.Data
{
    public class DriveImportConstants
    {
        public const string APP_NAME = "google-drive-import";

        public const string APP_TOKEN = "X-Vtex-Api-AppToken";
        public const string APP_KEY = "X-Vtex-Api-AppKey";

        public const string FORWARDED_HEADER = "X-Forwarded-For";
        public const string FORWARDED_HOST = "X-Forwarded-Host";
        public const string APPLICATION_JSON = "application/json";
        public const string APPLICATION_FORM = "application/x-www-form-urlencoded";
        public const string HEADER_VTEX_CREDENTIAL = "X-Vtex-Credential";
        public const string AUTHORIZATION_HEADER_NAME = "Authorization";
        public const string PROXY_AUTHORIZATION_HEADER_NAME = "Proxy-Authorization";
        public const string USE_HTTPS_HEADER_NAME = "X-Vtex-Use-Https";
        public const string PROXY_TO_HEADER_NAME = "X-Vtex-Proxy-To";
        public const string VTEX_ACCOUNT_HEADER_NAME = "X-Vtex-Account";
        public const string ENVIRONMENT = "vtexcommercestable";
        public const string LOCAL_ENVIRONMENT = "myvtex";
        public const string VTEX_ID_HEADER_NAME = "VtexIdclientAutCookie";
        public const string VTEX_WORKSPACE_HEADER_NAME = "X-Vtex-Workspace";
        public const string APP_SETTINGS = "vtex.google-drive-import";
        public const string ACCEPT = "Accept";
        public const string CONTENT_TYPE = "Content-Type";
        public const string HTTP_FORWARDED_HEADER = "HTTP_X_FORWARDED_FOR";

        public const string BUCKET = "google-drive";
        public const string CREDENTIALS = "google-credentials";
        public const string TOKEN = "google-token";
        public const string LOCK = "import-lock";
        public const string WATCH_EXPIRATION = "watch-expiration";

        public const string GOOGLE_AUTHORIZATION_URL = "https://accounts.google.com/o/oauth2/auth";
        public const string GOOGLE_TOKEN_URL = "https://accounts.google.com/o/oauth2/token";
        public const string GOOGLE_SCOPE = "https://www.googleapis.com/auth/drive";
        public const string GOOGLE_AUTHORIZATION_REVOKE = "https://oauth2.googleapis.com/revoke";
        public const string GOOGLE_WATCH = "https://www.googleapis.com/drive/v3/changes/watch";
        public const string GOOGLE_REPONSE_TYPE = "code";
        public const string GOOGLE_ACCESS_TYPE = "offline";
        public const string REDIRECT_SITE_BASE = "https://googleauth.myvtex.com";
        public const string REDIRECT_PATH = "return";

        public const string ADMIN_PAGE = "admin/google-drive-import";
        public const string WATCH_ENDPOINT = "google-drive-import/watch-notification";

        public const string GOOGLE_DRIVE_URL = "https://www.googleapis.com/drive/v3";
        public const string GOOGLE_DRIVE_URL_V2 = "https://www.googleapis.com/drive/v2";
        public const string GOOGLE_DRIVE_FILES = "files";

        public const string GRANT_TYPE_AUTH = "authorization_code";
        public const string GRANT_TYPE_REFRESH = "refresh_token";
        public const string WATCH_KIND = "api#channel";
        public const string WATCH_TYPE = "web_hook";

        public const string DATA_ENTITY = "DriveImport";
        public const string SCHEMA = "DriveImport";
        public const string PROCESSED_PREFIX = "Done";

        public class FolderNames
        {
            // Folder Structure:
            // Google Drive root
            // --VTEX Import
            // ----{VTEX Account}
            // ------Images
            // --------New
            // --------Error
            // --------Done

            public const string IMPORT = "VTEX Import";
            public const string IMAGES = "SKU Images";
            public const string NEW = "New";
            public const string ERROR = "Error";
            public const string DONE = "Done";
        }

        public class IdentificatorType
        {
            public const string SKU_ID = "SkuId";
            public const string SKU_REF_ID = "SkuRefId";
            public const string PRODUCT_REF_ID = "ProductRefId";
            public const string PRODUCT_ID = "ProductId";
        }
    }
}
