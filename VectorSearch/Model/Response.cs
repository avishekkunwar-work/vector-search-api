using System.Net;

namespace VectorSearch.Model
{
    public class Response<T>
    {
        public bool Success
        {
            get { return !Errors.Any(); }
        }
        public string Message { get; set; }
        public T Data { get; set; }
        public IList<Error> Errors { get; set; }
        public HttpStatusCode StatusCode { get; set; }
        public Response()
        {
            Errors = new List<Error>();
        }
    }

    public class Error
    {
        public string Message { get; set; }
        public string Type { get; set; }
    }
}
