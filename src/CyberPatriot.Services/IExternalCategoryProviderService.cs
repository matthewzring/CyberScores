using CyberPatriot.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CyberPatriot.Services
{

    public interface IExternalCategoryProviderService
    {
        Task InitializeAsync(IServiceProvider provider);

        /// <summary>
        /// Attempts to obtain the category for the given team. If a category is not available, returns null.
        /// </summary>
        ServiceCategory? GetCategory(TeamId team);
    }

}
