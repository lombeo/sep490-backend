﻿namespace Sep490_Backend.DTO.Authen
{
    public class SignUpDTO
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public bool Gender { get; set; }
    }
}
