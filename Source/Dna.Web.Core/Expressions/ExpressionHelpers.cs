using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Dna.Web.Core
{
    /// <summary>
    /// Helper methods for Expressions
    /// </summary>
    public static class ExpressionHelpers
    {
        /// <summary>
        /// Gets the value from an expression
        /// </summary>
        /// <typeparam name="T">The type of value to get from the expression</typeparam>
        /// <param name="lambda">The expression</param>
        /// <returns></returns>
        public static T GetPropertyValue<T>(this Expression<Func<T>> lambda)
        {
            return lambda.Compile().Invoke();
        }

        /// <summary>
        /// Get's the name of the property that is returned from the expression
        /// </summary>
        /// <param name="lambda">The expression</param>
        /// <returns></returns>
        public static string GetPropertyName<T>(this Expression<Func<T>> lambda)
        {
            return ((PropertyInfo)GetMemberExpression(lambda)?.Member)?.Name;
        }

        /// <summary>
        /// Sets the properties value contained in the expression
        /// </summary>
        /// <typeparam name="T">The type of property being set</typeparam>
        /// <param name="lambda">The expression</param>
        /// <param name="value">The value to set the property to</param>
        public static void SetPropertyValue<T>(this Expression<Func<T>> lambda, object value)
        {
            // Get the member expression
            var expression = GetMemberExpression(lambda);

            // Get the property info from the member
            var propertyInfo = (PropertyInfo)expression.Member;

            // Create a target value to set the property
            var target = Expression.Lambda(expression.Expression).Compile().DynamicInvoke();

            // Reflect to set the property value
            propertyInfo.SetValue(target, value);
        }

        /// <summary>
        /// Gets a <see cref="MemberExpression"/> from an <see cref="Expression"/>
        /// </summary>
        /// <param name="expression">The expression</param>
        /// <returns></returns>
        public static MemberExpression GetMemberExpression(Expression expression)
        {
            // If the expression is already a member expression...
            if (expression is MemberExpression)
                // Return it
                return (MemberExpression)expression;
            // If it is a lambda expression...
            else if (expression is LambdaExpression lambdaExpression)
            {
                // If the body is a member expression...
                if (lambdaExpression.Body is MemberExpression)
                    // Return body
                    return (MemberExpression)lambdaExpression.Body;
                // Else if the body is a unary expression...
                else if (lambdaExpression.Body is UnaryExpression unaryExpression)
                    // Return the operand of the unary as a member expression
                    return ((MemberExpression)unaryExpression.Operand);
            }

            // Unknown type, return null
            return null;
        }
    }
}