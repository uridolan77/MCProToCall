using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;

namespace ModelContextProtocol.Extensions.ErrorHandling
{
    /// <summary>
    /// Represents the result of an operation that can either succeed or fail
    /// </summary>
    /// <typeparam name="T">The type of the success value</typeparam>
    public readonly struct Result<T>
    {
        private readonly T _value;
        private readonly Exception _error;
        private readonly bool _isSuccess;

        private Result(T value)
        {
            _value = value;
            _error = null;
            _isSuccess = true;
        }

        private Result(Exception error)
        {
            _value = default;
            _error = error;
            _isSuccess = false;
        }

        /// <summary>
        /// Gets a value indicating whether the operation was successful
        /// </summary>
        public bool IsSuccess => _isSuccess;

        /// <summary>
        /// Gets a value indicating whether the operation failed
        /// </summary>
        public bool IsFailure => !_isSuccess;

        /// <summary>
        /// Gets the success value (throws if the operation failed)
        /// </summary>
        public T Value => _isSuccess ? _value : throw new InvalidOperationException("Cannot access value of failed result");

        /// <summary>
        /// Gets the error (throws if the operation succeeded)
        /// </summary>
        public Exception Error => !_isSuccess ? _error : throw new InvalidOperationException("Cannot access error of successful result");

        /// <summary>
        /// Creates a successful result
        /// </summary>
        public static Result<T> Success(T value) => new(value);

        /// <summary>
        /// Creates a failed result with an exception
        /// </summary>
        public static Result<T> Failure(Exception error) => new(error);

        /// <summary>
        /// Creates a failed result with an error message
        /// </summary>
        public static Result<T> Failure(string message) => new(new Exception(message));

        /// <summary>
        /// Maps the success value to a new type
        /// </summary>
        public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
        {
            return _isSuccess ? Result<TNew>.Success(mapper(_value)) : Result<TNew>.Failure(_error);
        }

        /// <summary>
        /// Maps the success value to a new type asynchronously
        /// </summary>
        public async Task<Result<TNew>> MapAsync<TNew>(Func<T, Task<TNew>> mapper)
        {
            return _isSuccess ? Result<TNew>.Success(await mapper(_value)) : Result<TNew>.Failure(_error);
        }

        /// <summary>
        /// Binds the result to another operation that returns a Result
        /// </summary>
        public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder)
        {
            return _isSuccess ? binder(_value) : Result<TNew>.Failure(_error);
        }

        /// <summary>
        /// Binds the result to another operation that returns a Result asynchronously
        /// </summary>
        public async Task<Result<TNew>> BindAsync<TNew>(Func<T, Task<Result<TNew>>> binder)
        {
            return _isSuccess ? await binder(_value) : Result<TNew>.Failure(_error);
        }

        /// <summary>
        /// Executes an action if the result is successful
        /// </summary>
        public Result<T> OnSuccess(Action<T> action)
        {
            if (_isSuccess)
                action(_value);
            return this;
        }

        /// <summary>
        /// Executes an action if the result is a failure
        /// </summary>
        public Result<T> OnFailure(Action<Exception> action)
        {
            if (!_isSuccess)
                action(_error);
            return this;
        }

        /// <summary>
        /// Gets the value or returns a default value if the operation failed
        /// </summary>
        public T GetValueOrDefault(T defaultValue = default)
        {
            return _isSuccess ? _value : defaultValue;
        }

        /// <summary>
        /// Gets the value or returns the result of a function if the operation failed
        /// </summary>
        public T GetValueOrDefault(Func<Exception, T> defaultValueFactory)
        {
            return _isSuccess ? _value : defaultValueFactory(_error);
        }

        /// <summary>
        /// Implicit conversion from T to Result&lt;T&gt;
        /// </summary>
        public static implicit operator Result<T>(T value) => Success(value);

        /// <summary>
        /// Implicit conversion from Exception to Result&lt;T&gt;
        /// </summary>
        public static implicit operator Result<T>(Exception error) => Failure(error);

        public override string ToString()
        {
            return _isSuccess ? $"Success({_value})" : $"Failure({_error?.Message})";
        }
    }

    /// <summary>
    /// Non-generic Result for operations that don't return a value
    /// </summary>
    public readonly struct Result
    {
        private readonly Exception _error;
        private readonly bool _isSuccess;

        private Result(bool isSuccess, Exception error = null)
        {
            _isSuccess = isSuccess;
            _error = error;
        }

        /// <summary>
        /// Gets a value indicating whether the operation was successful
        /// </summary>
        public bool IsSuccess => _isSuccess;

        /// <summary>
        /// Gets a value indicating whether the operation failed
        /// </summary>
        public bool IsFailure => !_isSuccess;

        /// <summary>
        /// Gets the error (throws if the operation succeeded)
        /// </summary>
        public Exception Error => !_isSuccess ? _error : throw new InvalidOperationException("Cannot access error of successful result");

        /// <summary>
        /// Creates a successful result
        /// </summary>
        public static Result Success() => new(true);

        /// <summary>
        /// Creates a failed result with an exception
        /// </summary>
        public static Result Failure(Exception error) => new(false, error);

        /// <summary>
        /// Creates a failed result with an error message
        /// </summary>
        public static Result Failure(string message) => new(false, new Exception(message));

        /// <summary>
        /// Executes an action if the result is successful
        /// </summary>
        public Result OnSuccess(Action action)
        {
            if (_isSuccess)
                action();
            return this;
        }

        /// <summary>
        /// Executes an action if the result is a failure
        /// </summary>
        public Result OnFailure(Action<Exception> action)
        {
            if (!_isSuccess)
                action(_error);
            return this;
        }

        /// <summary>
        /// Implicit conversion from Exception to Result
        /// </summary>
        public static implicit operator Result(Exception error) => Failure(error);

        public override string ToString()
        {
            return _isSuccess ? "Success" : $"Failure({_error?.Message})";
        }
    }

    /// <summary>
    /// Extension methods for Result types
    /// </summary>
    public static class ResultExtensions
    {
        /// <summary>
        /// Combines multiple results into a single result
        /// </summary>
        public static Result Combine(params Result[] results)
        {
            foreach (var result in results)
            {
                if (result.IsFailure)
                    return result;
            }
            return Result.Success();
        }

        /// <summary>
        /// Combines multiple results into a single result with values
        /// </summary>
        public static Result<T[]> Combine<T>(params Result<T>[] results)
        {
            var values = new T[results.Length];
            for (int i = 0; i < results.Length; i++)
            {
                if (results[i].IsFailure)
                    return Result<T[]>.Failure(results[i].Error);
                values[i] = results[i].Value;
            }
            return Result<T[]>.Success(values);
        }

        /// <summary>
        /// Converts a Task&lt;T&gt; to a Task&lt;Result&lt;T&gt;&gt;
        /// </summary>
        public static async Task<Result<T>> ToResultAsync<T>(this Task<T> task)
        {
            try
            {
                var result = await task;
                return Result<T>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<T>.Failure(ex);
            }
        }

        /// <summary>
        /// Converts a Task to a Task&lt;Result&gt;
        /// </summary>
        public static async Task<Result> ToResultAsync(this Task task)
        {
            try
            {
                await task;
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(ex);
            }
        }
    }
}
