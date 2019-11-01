﻿// © 2019 Mobsites. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Mobsites.AspNetCore.Identity.Cosmos
{
    /// <summary>
    ///     Represents a new instance of a persistence store for the identity user roles.
    /// </summary>
    /// <typeparam name="TUserRole">The type representing a user role.</typeparam>
    public class UserRoles<TUserRole> : IUserRoles<TUserRole>
        where TUserRole : IdentityUserRole, new()
    {
        #region Setup

        private readonly ICosmos cosmos;

        /// <summary>
        ///     Constructs a new instance of <see cref="UserTokens{TUserToken}"/>.
        /// </summary>
        /// <param name="cosmos">The context in which to access the Cosmos Container for the identity store.</param>
        public UserRoles(ICosmos cosmos)
        {
            this.cosmos = cosmos ?? throw new ArgumentNullException(nameof(cosmos));
        }

        #endregion

        #region Add UserRole

        /// <summary>
        ///     Adds the given <paramref name="userRole"/> to the store.
        /// </summary>
        /// <param name="userRole">The user role to add.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        ///     The <see cref="Task"/> that represents the asynchronous operation.
        /// </returns>
        public async Task AddAsync(TUserRole userRole, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (userRole != null)
            {
                try
                {
                    var partitionKey = string.IsNullOrEmpty(userRole.PartitionKey) ? PartitionKey.None : new PartitionKey(userRole.PartitionKey);

                    await cosmos.IdentityContainer.CreateItemAsync(userRole, partitionKey, cancellationToken: cancellationToken);
                }
                catch (CosmosException)
                {

                }
            }
        }

        #endregion

        #region Remove UserRole

        /// <summary>
        ///     Removes the given <paramref name="userRole"/> from the store.
        /// </summary>
        /// <param name="userRole">The user role to remove.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        ///     The <see cref="Task"/> that represents the asynchronous operation.
        /// </returns>
        public async Task RemoveAsync(TUserRole userRole, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (userRole != null)
            {
                try
                {
                    var partitionKey = string.IsNullOrEmpty(userRole.PartitionKey) ? PartitionKey.None : new PartitionKey(userRole.PartitionKey);

                    await cosmos.IdentityContainer.DeleteItemAsync<TUserRole>(userRole.Id, partitionKey, cancellationToken: cancellationToken);
                }
                catch (CosmosException)
                {

                }
            }
        }

        #endregion

        #region Get Role Names

        /// <summary>
        ///     Retrieves a list of the role names from the store that the user with the specified <paramref name="userId"/> is a member of.
        /// </summary>
        /// <param name="userId">The user's id.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The list of role names if any.</returns>
        public async Task<IList<string>> GetRoleNamesAsync<TUser>(string userId, CancellationToken cancellationToken)
            where TUser : IdentityUser, new()
        {
            IList<string> roleNames = new List<string>();

            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrEmpty(userId))
            {
                try
                {
                    var user = new TUser();
                    var partitionKey = string.IsNullOrEmpty(user.PartitionKey) ? PartitionKey.None : new PartitionKey(user.PartitionKey);

                    user =  await cosmos.IdentityContainer.ReadItemAsync<TUser>(userId, partitionKey, cancellationToken: cancellationToken);

                    if (!string.IsNullOrEmpty(user.FlattenRoleNames))
                    {
                        foreach (var roleName in user.FlattenRoleNames.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            roleNames.Add(roleName);
                        }
                    }
                }
                catch (CosmosException)
                {

                }
            }

            return roleNames;
        }

        #endregion

        #region Get Role Users

        /// <summary>
        ///     Retrieves a list of users from the store that belong to the role with the specified <paramref name="roleId"/>.
        /// </summary>
        /// <param name="roleId">The role's id.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The list of users if any.</returns>
        public async Task<IList<TUser>> GetUsersAsync<TUser>(string roleId, CancellationToken cancellationToken)
            where TUser : IdentityUser, new()
        {
            IList<TUser> users = new List<TUser>();

            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrEmpty(roleId))
            {
                try
                {
                    var partitionKey = new TUser().PartitionKey;

                    // LINQ query generation
                    var feedIterator = cosmos.IdentityContainer
                        .GetItemLinqQueryable<TUser>(requestOptions: new QueryRequestOptions
                        {
                            PartitionKey = string.IsNullOrEmpty(partitionKey) ? PartitionKey.None : new PartitionKey(partitionKey)
                        })
                        .Where(user => !string.IsNullOrEmpty(user.FlattenRoleIds) && user.FlattenRoleIds.Contains(roleId))
                        .ToFeedIterator();

                    //Asynchronous query execution
                    while (feedIterator.HasMoreResults)
                    {
                        foreach (var user in await feedIterator.ReadNextAsync())
                        {
                            users.Add(user);
                        }
                    }
                }
                catch (CosmosException)
                {

                }
            }

            return users;
        }

        #endregion

        #region Find UserRole

        /// <summary>
        ///     Retrieves a user role from the store for the given <paramref name="userId"/> and <paramref name="roleId"/> if it exists.
        /// </summary>
        /// <param name="userId">The user's id.</param>
        /// <param name="roleId">The role's id.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The user role if it exists.</returns>
        public async Task<TUserRole> FindAsync(string userId, string roleId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var partitionKey = new TUserRole().PartitionKey;

                // LINQ query generation
                var feedIterator = cosmos.IdentityContainer
                    .GetItemLinqQueryable<TUserRole>(requestOptions: new QueryRequestOptions
                    {
                        PartitionKey = string.IsNullOrEmpty(partitionKey) ? PartitionKey.None : new PartitionKey(partitionKey)
                    })
                    .Where(userRole => userRole.UserId == userId && userRole.RoleId == roleId)
                    .ToFeedIterator();

                //Asynchronous query execution
                while (feedIterator.HasMoreResults)
                {
                    // Should only be one, so...
                    return (await feedIterator.ReadNextAsync()).First();
                }
            }
            catch (CosmosException)
            {

            }

            return null;
        }

        #endregion
    }
}
