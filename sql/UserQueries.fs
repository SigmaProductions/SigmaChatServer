namespace SigmaChatServer

module UserQueries =
    open Microsoft.AspNetCore.Http
    open Giraffe
    open System.Data
    open Dapper
    open SigmaChatServer.Models
    open System

    let createUser (ctx: HttpContext) (userId: string) =
        task {
            use connection = ctx.GetService<IDbConnection>()

            let sql =
                """INSERT INTO "Users" ("Id") 
                VALUES (@userId) RETURNING *;"""

            let sqlParams = {| userId = userId |}

            let! user = connection.QueryFirstAsync<User>(sql, sqlParams)
            return (user)
        }

    let updateUser (ctx: HttpContext) (userId: string) (model: UpdateMeModel) =
        task {
            use connection = ctx.GetService<IDbConnection>()

            let sql =
                """UPDATE "Users" SET "Nickname" = @nickname
                WHERE "Id" = @userId; """

            let sqlParams =
                {| userId = userId
                   nickname = model.Nickname |}

            let! _ = connection.ExecuteAsync(sql, sqlParams)
            return ()
        }

    let getUser (ctx: HttpContext) (userId: string) =
        task {
            use connection = ctx.GetService<IDbConnection>()

            let sql =
                """SELECT "Users".*, "UserProfilePictures"."BlobName" as "ProfilePictureBlob" FROM "Users" 
                LEFT JOIN "UserProfilePictures" ON "Id" = "UserId"
                WHERE "Id" = @userId;"""

            let sqlParams = {| userId = userId |}

            let! user = connection.QueryFirstOrDefaultAsync<User>(sql, sqlParams)

            let optioned =
                match box user with
                | null -> None
                | _ -> Some user

            return optioned
        }

    let getAllUserIds (ctx: HttpContext) =
        task {
            use connection = ctx.GetService<IDbConnection>()

            let sql = """SELECT "Id" FROM "Users";"""

            let! userIds = connection.QueryAsync<string>(sql)
            return userIds
        }

    type ProfilePictureModel =
        { UserId: string
          BlobName: string
          OriginalFilename: string }

    let upsertProfilePicture (ctx: HttpContext) (model: ProfilePictureModel) =
        task {
            use connection = ctx.GetService<IDbConnection>()

            let sql =
                """
                INSERT INTO "UserProfilePictures" ("UserId", "BlobName", "OriginalFilename", "DateCreated")
                VALUES (@userId, @blobName, @originalFilename, NOW())
                ON CONFLICT ("UserId") DO UPDATE
                    SET "BlobName" = EXCLUDED."BlobName",
                        "OriginalFilename" = EXCLUDED."OriginalFilename",
                        "DateCreated" = NOW();
            """

            return! connection.QueryAsync(sql, model)
        }