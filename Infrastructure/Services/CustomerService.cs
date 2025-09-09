
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Infrastructure.Services;

public class CustomerService
{
	private readonly CustomerRepository _customerRepository;
	private readonly ILogger<CustomerService> _logger;
	private readonly PriceLevelRepository _priceLevelRepository;
	private readonly SalesManagerRepository _salesManagerRepository;
	public CustomerService(CustomerRepository customerRepository, PriceLevelRepository priceLevelRepository, SalesManagerRepository salesManagerRepository, ILogger<CustomerService> logger)
	{
		_customerRepository = customerRepository;
		_logger = logger;
		_priceLevelRepository = priceLevelRepository;
		_salesManagerRepository = salesManagerRepository;
	}

	public async Task<CustomerEntity> AddCustomerAsync(CustomerEntity customer)
	{
		try
		{
			var existingCustomer = await _customerRepository.GetOneAsync(c => c.Id == customer.Id);

			if (existingCustomer is not null)
			{
				// Update existing customer
				await UpdateCustomerAsync(customer);
			}
			else
			{
				// Add new customer
				var allCustomers = await _customerRepository.GetAllAsync();

				// Generate and assign ID
				customer.Id = GenerateNextCustomerId(allCustomers);
				await _customerRepository.AddAsync(customer);
			}

			return customer;
		}
		catch (Exception ex)
		{
			_logger.LogError($"Error getting/adding customer: {ex.Message}");
			Debug.WriteLine($"Error getting/adding customer: {ex.Message}");
			return null!;
		}
	}

	private string GenerateNextCustomerId(IEnumerable<CustomerEntity> existingCustomers)
	{
		var maxNumber = existingCustomers
			.Select(c => c.Id)
			.Where(id => id.StartsWith("N "))
			.Select(id => id.Substring(2)) // Get "001", "002", etc.
			.Where(str => int.TryParse(str, out _)) // Filter out invalid ones
			.Select(int.Parse)
			.DefaultIfEmpty(0)
			.Max();

		int nextNumber = maxNumber + 1;
		return $"N {nextNumber:000}"; // Formats like N 001, N 002
	}


	public async Task<CustomerEntity> GetCustomerAsync(string fulName)
	{
		try
		{
			return await _customerRepository.GetOneAsync(b => b.FullName == fulName);
		}
		catch (Exception ex)
		{
			_logger.LogError($"Error getting/adding customer: {ex.Message}");
			Debug.WriteLine($"Error getting/adding customer: {ex.Message}");
			return null!;
		}
	}


	public async Task<IEnumerable<CustomerEntity>> GetAllCustomersAsync()
	{
		try
		{
			var customerEntities = await _customerRepository.GetAllAsync();
			return customerEntities ?? Enumerable.Empty<CustomerEntity>();
		}
		catch (Exception ex)
		{
			_logger.LogError($"Error getting customers: {ex.Message}");
			Debug.WriteLine($"Error getting customers: {ex.Message}");
			return Enumerable.Empty<CustomerEntity>();
		}
	}

	public async Task<CustomerEntity> UpdateCustomerAsync(CustomerEntity customer)
	{
		try
		{
			return await _customerRepository.UpdateAsync(c => c.Id == customer.Id, customer);
		}
		catch (Exception ex)
		{
			_logger.LogError($"Error in updating product: {ex.Message}");
			Debug.WriteLine($"Error in updating product: {ex.Message}");
			return null!;
		}
	}

	public async Task<bool> DeleteCustomerAsync(CustomerEntity customer)
	{
		try
		{
			var existingCustomer = await _customerRepository.GetOneAsync(b => b.Id == customer.Id);
			if (existingCustomer != null)
			{
				await _customerRepository.RemoveAsync(customer);
				return true;
			}
			else
			{
				return false;
			}
		}
		catch (Exception ex)
		{
			_logger.LogError($"Error deleting customer: {ex.Message}");
			Debug.WriteLine($"Error deleting customer: {ex.Message}");
			return false;
		}
	}

	public async Task<IEnumerable<PriceLevelEntity>> GetAllPriceLevelsAsync()
	{
		try
		{
			var levels = await _priceLevelRepository.GetAllAsync();
			return levels ?? Enumerable.Empty<PriceLevelEntity>();
		}
		catch (Exception ex)
		{
			_logger.LogError($"Error getting price levels: {ex.Message}");
			Debug.WriteLine($"Error getting price levels: {ex.Message}");
			return Enumerable.Empty<PriceLevelEntity>();
		}
	}

	public async Task<IEnumerable<SalesManagerEntity>> GetAllSalesManagersAsync()
	{
		try
		{
			var result = await _salesManagerRepository.GetAllAsync();
			return result ?? Enumerable.Empty<SalesManagerEntity>();
		}
		catch (Exception ex)
		{
			_logger.LogError($"Error getting sales managers: {ex.Message}");
			Debug.WriteLine($"Error getting sales managers: {ex.Message}");
			return Enumerable.Empty<SalesManagerEntity>();
		}
	}


}
