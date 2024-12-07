namespace UnitTestEx.Azure.Functions
{
    /// <summary>
    /// Represents the route check option for the <see cref="HttpTriggerTester{TFunction}"/>.
    /// </summary>
    public enum RouteCheckOption
    {
        /// <summary>
        /// No route check is required,
        /// </summary>
        None,

        /// <summary>
        /// The route should match the specified path excluding any query string.
        /// </summary>
        Path,

        /// <summary>
        /// The route should match the specified path including the query string.
        /// </summary>
        PathAndQuery,

        /// <summary>
        /// The route should start with the specified path and query string.
        /// </summary>
        PathAndQueryStartsWith,

        /// <summary>
        /// The route query (ignore path) should match the specified query string.
        /// </summary>
        Query,

        /// <summary>
        /// The route query (ignore path) should start with the specified query string.
        /// </summary>
        QueryStartsWith
    }
}